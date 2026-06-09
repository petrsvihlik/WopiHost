using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WopiHost.E2ETests.Collabora.Fixtures;

/// <summary>
/// xUnit class fixture that owns the distributed application for the e2e suite. Boots the
/// AppHost programmatically with <c>AppHost:UseCollabora=true</c>, waits for the Collabora
/// container and the WOPI host to become healthy, and exposes the frontend URL for tests
/// to drive Playwright against.
/// </summary>
/// <remarks>
/// <para>
/// Cold-start cost is on the order of 10–20 seconds (Collabora pull on a clean cache is longer
/// — first CI run typically pays 60+ s). Fixture lifetime is the whole test collection so the
/// cost amortises across every test in <see cref="CollaboraFixtureCollection"/>.
/// </para>
/// <para>
/// Docker is required. The fixture probes <c>docker info</c> at construction time and stores
/// the result in <see cref="IsDockerAvailable"/>; tests check this and skip when the engine is
/// unreachable. <see cref="InitializeAsync"/> does NOT throw on missing Docker — that
/// would surface as a hard failure rather than a skip on contributor machines.
/// </para>
/// <para>
/// Redis is explicitly disabled (<c>AppHost:UseRedisLocks=false</c>) so the e2e test cycle
/// only depends on a single container (Collabora) rather than two. The memory lock provider
/// is sufficient for the single-process happy-path scenarios covered here.
/// </para>
/// </remarks>
public sealed class CollaboraAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public bool IsDockerAvailable { get; } = DockerCheck.IsDockerAvailable();

    /// <summary>
    /// HTTPS URL of the <c>wopihost-web-collabora</c> resource after the application has been started.
    /// Populated by <see cref="InitializeAsync"/>; reading before initialisation completes
    /// throws.
    /// </summary>
    public Uri WebFrontendUrl { get; private set; } = null!;

    /// <summary>
    /// Filesystem path to the writable <c>sample/wopi-docs</c> tree the sample WOPI host serves.
    /// Tests assert on file timestamps / sizes here to verify that PutFile callbacks from
    /// Collabora actually round-tripped back through the host to disk.
    /// </summary>
    public string WopiDocsPath { get; } = ResolveWopiDocsPath();

    public async ValueTask InitializeAsync()
    {
        if (!IsDockerAvailable)
        {
            // Start nothing — tests inspect IsDockerAvailable and skip. Throwing here would
            // surface as a fixture init failure (red) rather than a clean skip.
            return;
        }

        // Feed AppHost:UseCollabora / AppHost:UseRedisLocks via HostApplicationBuilderSettings.
        // DistributedApplicationTestingBuilder.CreateAsync runs the AppHost's Program.cs
        // (resource-graph discovery) BEFORE returning, so post-CreateAsync configuration
        // mutations on the returned builder are too late to flip the resource graph. The
        // configureBuilder overload is the documented seam — its callback fires before the
        // AppHost reads `builder.Configuration.GetValue<bool>(...)`. Command-line --args silently
        // no-op here (the resource graph keeps Redis on, Collabora off).
        var appBuilder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.WopiHost_AppHost>([], (_, settings) =>
            {
                settings.Configuration ??= new ConfigurationManager();
                settings.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AppHost:UseCollabora"] = "true",
                    // ONLYOFFICE defaults on in the AppHost; keep it off here so this suite spins up
                    // only the Collabora container, not the ~4.3 GB ONLYOFFICE one alongside it.
                    ["AppHost:UseOnlyOffice"] = "false",
                    ["AppHost:UseRedisLocks"] = "false",
                });
            })
            .ConfigureAwait(false);

        // Sanity check: if the AppHost:UseCollabora flag didn't propagate (args and
        // `appBuilder.Configuration[..]="true"` *after* CreateAsync silently no-op because the
        // AppHost has already read the value during entry-point invocation; the configureBuilder
        // callback is the only seam that runs *before* that read), the Collabora container won't
        // be in the resource graph. Fail fast here rather than wait through the timeouts below.
        var resourceNames = appBuilder.Resources.Select(r => r.Name).ToList();
        if (!resourceNames.Contains("collabora"))
        {
            throw new InvalidOperationException(
                $"Collabora resource not found in the AppHost graph after CreateAsync. Resources: [{string.Join(", ", resourceNames)}]. " +
                $"AppHost:UseCollabora in config = '{appBuilder.Configuration["AppHost:UseCollabora"] ?? "<unset>"}'. " +
                "The configureBuilder callback didn't apply the in-memory configuration source before the AppHost's Program.cs ran.");
        }

        _app = await appBuilder.BuildAsync().ConfigureAwait(false);
        await _app.StartAsync().ConfigureAwait(false);

        // Wait for the resources whose readiness gates the test. Collabora has an HTTP health
        // check (/hosting/discovery) wired in the AppHost, so WaitForResourceHealthyAsync is
        // the right blocker. The frontend's readiness already implies the backend is up (it
        // WaitFors the backend internally), so the wait targets the chain end.
        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)); // generous for first-run image pull

        // Wait for each resource to be reported in a Running state by Aspire's resource
        // notification service. Note that Running != accepting traffic — Aspire's Running
        // signal only means the orchestrator has issued the start command. The explicit HTTP
        // poll against Collabora's /hosting/discovery below backstops this to confirm the
        // container is actually serving.
        await notifications.WaitForResourceAsync("collabora", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);
        await notifications.WaitForResourceAsync("wopihost-collabora", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);
        await notifications.WaitForResourceAsync("wopihost-web-collabora", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);

        // Belt-and-braces probe: poll Collabora's /hosting/discovery until it serves a 200,
        // or fail the fixture with a diagnostic message that beats waiting through the
        // downstream Playwright timeout.
        //
        // Use the Aspire-discovered endpoint, NOT the hardcoded localhost:9980 the AppHost
        // requests. Under DistributedApplicationTestingBuilder, DCP can remap the host port
        // even when the AppHost asks for a specific one — `app.GetEndpoint("collabora")` is
        // the only source of truth that survives that remap.
        // The endpoint name "collabora" is the one passed to WithHttpEndpoint(..., name: "collabora")
        // in the AppHost. GetEndpoint(resourceName) alone defaults to an empty-string endpoint
        // name and fails with "Endpoint '' for resource 'collabora' not found" — the resource
        // and endpoint happen to share the name "collabora" here.
        var collaboraEndpoint = _app.GetEndpoint("collabora", "collabora");
        var discoveryUrl = new Uri(collaboraEndpoint, "/hosting/discovery");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var collaboraDeadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTime.UtcNow < collaboraDeadline)
        {
            try
            {
                using var response = await http.GetAsync(discoveryUrl, cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
                // Connection refused / DNS — Collabora not yet serving. Keep polling.
            }
            catch (TaskCanceledException) when (!cts.Token.IsCancellationRequested)
            {
                // Per-request HttpClient.Timeout elapsed (Collabora not yet bound). Distinct
                // from cts.Token cancellation — that one means the overall deadline was hit.
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token).ConfigureAwait(false);
        }
        if (DateTime.UtcNow >= collaboraDeadline)
        {
            throw new InvalidOperationException(
                $"Collabora's /hosting/discovery did not respond 200 within 2 minutes.\n" +
                $"Aspire-discovered endpoint: {collaboraEndpoint}\n" +
                $"Probed URL:                 {discoveryUrl}\n" +
                $"Containers visible to docker:\n{await CaptureDockerPsAsync().ConfigureAwait(false)}\n" +
                "If `docker ps -a` shows no collabora-* row, DCP isn't actually starting containers " +
                "in this test-mode run. If it shows a row with status 'Exited', inspect its logs.");
        }

        WebFrontendUrl = new Uri(_app.GetEndpoint("wopihost-web-collabora", "https").ToString());
    }

    private static async Task<string> CaptureDockerPsAsync()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps -a --format {{.Names}}\\t{{.Image}}\\t{{.Status}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return "<docker process failed to start>";
            }
            var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            return stdout.Trim().Length == 0 ? "<no containers>" : stdout;
        }
        catch (Exception ex)
        {
            return $"<docker ps failed: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    /// <summary>
    /// Captures the most recent stdout/stderr from the running Collabora container so failure
    /// diagnostics include coolwsd's own logs. Used by both tests to surface auth / WOPI
    /// rejection reasons that aren't visible from the iframe's postMessage trail.
    /// </summary>
    /// <remarks>
    /// The output is shaped as two sections:
    /// <list type="bullet">
    /// <item>A <b>filtered</b> view that keeps only lines whose content matches one of the
    /// diagnostic keywords (WOPI / WebSocket / auth / reject / etc.). This is the section that
    /// answers "why was the request denied?" without being drowned out by Collabora's per-tile
    /// trace spam.</item>
    /// <item>The <b>full tail</b> of the container's combined output, capped at
    /// <paramref name="tailLines"/>. Kept as a fallback for cases where the filter misses the
    /// signal (e.g. an unrecognised error string).</item>
    /// </list>
    /// </remarks>
    public async Task<string> CaptureCollaboraLogsAsync(int tailLines = 2000)
    {
        try
        {
            // First find the container name. Aspire names containers after the resource with a
            // hash suffix; match any running "collabora*". Defensive fallback: if name discovery
            // fails, dump *all* running containers' logs (only one in this setup).
            using var psProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps --filter ancestor=collabora/code --format {{.Names}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (psProcess is null)
            {
                return "<docker not available>";
            }
            var names = (await psProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false)).Trim();
            await psProcess.WaitForExitAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(names))
            {
                return "<no running collabora/code container>";
            }
            var containerName = names.Split('\n')[0].Trim();

            using var logsProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"logs --tail {tailLines} {containerName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (logsProcess is null)
            {
                return $"<failed to start `docker logs {containerName}`>";
            }
            // Collabora writes most diagnostics to stderr, so merge both streams.
            var stdoutTask = logsProcess.StandardOutput.ReadToEndAsync();
            var stderrTask = logsProcess.StandardError.ReadToEndAsync();
            await logsProcess.WaitForExitAsync().ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            // Lines relevant to diagnosing websocket / WOPI-host rejection:
            //   - "WOPI" / "wopi" — matches host strings, allowed-host checks, WopiSrc parsing
            //   - "WebSocket" / "websocket" — handshake + upgrade
            //   - "Unauthorized" / "unauthoriz" — coolwsd's literal denial message
            //   - "domain" — the regex it's matching against
            //   - "host" — host header parsing (case-sensitive to avoid matching every URL)
            //   - "ERR" / "WRN" — coolwsd's own severity tags
            //   - "Reject" / "reject" — generic denial
            //   - "isWopiHostAllowed" / "FrameAncestors" — specific check names from coolwsd
            // Kept narrow on purpose; the full tail below catches anything missed.
            var filterKeywords = new[]
            {
                "WOPI", "wopi", "WebSocket", "websocket", "Unauthorized", "unauthoriz",
                "ERR", "WRN", "Reject", "reject", "isWopiHostAllowed", "FrameAncestors",
                "frame-ancestors", "allowed", "Allow", "denied", "Denied",
            };

            static IEnumerable<string> Filter(string text, string[] keywords)
            {
                foreach (var line in text.Split('\n'))
                {
                    foreach (var kw in keywords)
                    {
                        if (line.Contains(kw, StringComparison.Ordinal))
                        {
                            yield return line;
                            break;
                        }
                    }
                }
            }

            var filteredStdout = string.Join("\n", Filter(stdout, filterKeywords));
            var filteredStderr = string.Join("\n", Filter(stderr, filterKeywords));

            // Truncate the full-tail dump to a budget that fits in xUnit's failure message
            // without truncating off the more useful filtered section. xUnit truncates messages
            // ~around 32 KB by default; budget ~6 KB per stream for the raw tail.
            static string TailBudget(string text, int maxChars)
            {
                if (text.Length <= maxChars) return text;
                return "…(truncated)…\n" + text[^maxChars..];
            }

            return $"=== {containerName} filtered stdout ===\n{filteredStdout}\n" +
                   $"=== {containerName} filtered stderr ===\n{filteredStderr}\n" +
                   $"=== {containerName} stdout tail ({tailLines} max) ===\n{TailBudget(stdout, 6000)}\n" +
                   $"=== {containerName} stderr tail ({tailLines} max) ===\n{TailBudget(stderr, 6000)}";
        }
        catch (Exception ex)
        {
            return $"<CaptureCollaboraLogsAsync threw: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string ResolveWopiDocsPath()
    {
        // sample/WopiHost/appsettings.json sets WopiHost.FileSystemProvider:RootPath="../wopi-docs".
        // The path is relative to the *sample's* directory at runtime, so absolute = repo-root/sample/wopi-docs.
        // Walks up from the test binary location until the repo root (the directory containing
        // WOPI.slnx), then points at sample/wopi-docs from there. Robust to the artifacts/ layout
        // (UseArtifactsOutput) which puts test dlls several levels below.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WOPI.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root (looking for WOPI.slnx) from " +
                AppContext.BaseDirectory + ".");
        }
        return Path.Combine(dir.FullName, "sample", "wopi-docs");
    }
}
