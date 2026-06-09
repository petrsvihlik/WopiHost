using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WopiHost.E2ETests.OnlyOffice.Fixtures;

/// <summary>
/// xUnit class fixture that owns the distributed application for the ONLYOFFICE e2e suite. Boots
/// the AppHost programmatically with <c>AppHost:UseOnlyOffice=true</c> (and Collabora + Redis off),
/// waits for the ONLYOFFICE container and the WOPI host to become healthy, and exposes the
/// ONLYOFFICE frontend URL for tests to drive Playwright against.
/// </summary>
/// <remarks>
/// <para>
/// The ONLYOFFICE image is heavyweight (bundles its own PostgreSQL / RabbitMQ), so the cold-start
/// is longer than Collabora's — first CI run on a clean cache pays the ~4.3 GB pull plus the
/// document-engine warmup. Fixture lifetime is the whole test collection so the cost amortises.
/// </para>
/// <para>
/// Collabora is explicitly disabled (<c>AppHost:UseCollabora=false</c>) so this suite spins up only
/// the ONLYOFFICE lane (its own backend <c>wopihost-onlyoffice</c> + frontend
/// <c>wopihost-web-onlyoffice</c>) rather than both editors. Redis is disabled
/// (<c>AppHost:UseRedisLocks=false</c>) so the cycle depends on a single container; the memory lock
/// provider is sufficient for the single-process happy-path scenario covered here.
/// </para>
/// <para>
/// Docker is required. The fixture probes <c>docker info</c> at construction time and stores the
/// result in <see cref="IsDockerAvailable"/>; tests check this and skip when the engine is
/// unreachable. <see cref="InitializeAsync"/> does NOT throw on missing Docker.
/// </para>
/// </remarks>
public sealed class OnlyOfficeAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public bool IsDockerAvailable { get; } = DockerCheck.IsDockerAvailable();

    /// <summary>
    /// HTTPS URL of the <c>wopihost-web-onlyoffice</c> resource after the application has started.
    /// Populated by <see cref="InitializeAsync"/>; reading before initialisation completes throws.
    /// </summary>
    public Uri WebFrontendUrl { get; private set; } = null!;

    /// <summary>
    /// Filesystem path to the writable <c>sample/wopi-docs</c> tree the sample WOPI host serves.
    /// </summary>
    public string WopiDocsPath { get; } = ResolveWopiDocsPath();

    public async ValueTask InitializeAsync()
    {
        if (!IsDockerAvailable)
        {
            // Start nothing — tests inspect IsDockerAvailable and skip. Throwing here would surface
            // as a fixture init failure (red) rather than a clean skip.
            return;
        }

        // Feed the AppHost flags via HostApplicationBuilderSettings. DistributedApplicationTestingBuilder
        // .CreateAsync runs the AppHost's Program.cs (resource-graph discovery) BEFORE returning, so
        // post-CreateAsync configuration mutations are too late to flip the resource graph. The
        // configureBuilder overload is the documented seam — its callback fires before the AppHost
        // reads builder.Configuration.GetValue<bool>(...).
        var appBuilder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.WopiHost_AppHost>([], (_, settings) =>
            {
                settings.Configuration ??= new ConfigurationManager();
                settings.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AppHost:UseOnlyOffice"] = "true",
                    ["AppHost:UseCollabora"] = "false",
                    ["AppHost:UseRedisLocks"] = "false",
                });
            })
            .ConfigureAwait(false);

        // Fail fast if the flag didn't propagate (the ONLYOFFICE container won't be in the graph)
        // rather than wait through the timeouts below.
        var resourceNames = appBuilder.Resources.Select(r => r.Name).ToList();
        if (!resourceNames.Contains("onlyoffice"))
        {
            throw new InvalidOperationException(
                $"ONLYOFFICE resource not found in the AppHost graph after CreateAsync. Resources: [{string.Join(", ", resourceNames)}]. " +
                $"AppHost:UseOnlyOffice in config = '{appBuilder.Configuration["AppHost:UseOnlyOffice"] ?? "<unset>"}'. " +
                "The configureBuilder callback didn't apply the in-memory configuration source before the AppHost's Program.cs ran.");
        }

        _app = await appBuilder.BuildAsync().ConfigureAwait(false);
        await _app.StartAsync().ConfigureAwait(false);

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // generous for first-run image pull + engine warmup

        await notifications.WaitForResourceAsync("onlyoffice", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);
        await notifications.WaitForResourceAsync("wopihost-onlyoffice", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);
        await notifications.WaitForResourceAsync("wopihost-web-onlyoffice", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);

        // Belt-and-braces probe: poll ONLYOFFICE's /healthcheck until it returns 200 with body
        // "true". Unlike /hosting/discovery (served by nginx almost immediately), /healthcheck only
        // answers true once the document-conversion engine is actually ready — gating on it removes
        // the "editor shell loads but document download fails" cold-start race.
        //
        // Use the Aspire-discovered endpoint, NOT the hardcoded localhost:9981 the AppHost requests:
        // under DistributedApplicationTestingBuilder, DCP can remap the host port even when the
        // AppHost asks for a specific one. The endpoint name "onlyoffice" is the one passed to
        // WithHttpEndpoint(..., name: "onlyoffice") in the AppHost.
        var onlyOfficeEndpoint = _app.GetEndpoint("onlyoffice", "onlyoffice");
        var healthUrl = new Uri(onlyOfficeEndpoint, "/healthcheck");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        var healthy = false;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync(healthUrl, cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = (await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false)).Trim();
                    if (body.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        healthy = true;
                        break;
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Connection refused / DNS — ONLYOFFICE not yet serving. Keep polling.
            }
            catch (TaskCanceledException) when (!cts.Token.IsCancellationRequested)
            {
                // Per-request HttpClient.Timeout elapsed (engine not yet bound). Distinct from the
                // overall-deadline cancellation on cts.Token.
            }
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token).ConfigureAwait(false);
        }
        if (!healthy)
        {
            throw new InvalidOperationException(
                $"ONLYOFFICE's /healthcheck did not return 200 'true' within 3 minutes.\n" +
                $"Aspire-discovered endpoint: {onlyOfficeEndpoint}\n" +
                $"Probed URL:                 {healthUrl}\n" +
                $"Containers visible to docker:\n{await CaptureDockerPsAsync().ConfigureAwait(false)}");
        }

        WebFrontendUrl = new Uri(_app.GetEndpoint("wopihost-web-onlyoffice", "https").ToString());
    }

    private static async Task<string> CaptureDockerPsAsync()
    {
        try
        {
            var (stdout, _) = await RunProcessAsync("docker", "ps -a --format {{.Names}}\\t{{.Image}}\\t{{.Status}}").ConfigureAwait(false);
            return stdout.Trim().Length == 0 ? "<no containers>" : stdout;
        }
        catch (Exception ex)
        {
            return $"<docker ps failed: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    /// <summary>
    /// Captures the ONLYOFFICE container's logs for failure diagnostics: the docker-logs tail plus a
    /// tail of the internal docservice/converter logs (where WOPI download / proof / edit-mode
    /// errors are actually written — they don't surface on the container's stdout).
    /// </summary>
    public async Task<string> CaptureOnlyOfficeLogsAsync(int tailLines = 200)
    {
        try
        {
            var (names, _) = await RunProcessAsync("docker", "ps --filter ancestor=onlyoffice/documentserver --format {{.Names}}").ConfigureAwait(false);
            var container = names.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(container))
            {
                return "<no running onlyoffice/documentserver container>";
            }

            var (dockerLogs, dockerLogsErr) = await RunProcessAsync("docker", $"logs --tail {tailLines} {container}").ConfigureAwait(false);
            // The interesting WOPI diagnostics live in the docservice logs inside the container.
            var (docservice, _) = await RunProcessAsync(
                "docker",
                $"exec {container} sh -lc \"tail -n {tailLines} /var/log/onlyoffice/documentserver/docservice/out.log /var/log/onlyoffice/documentserver/docservice/err.log /var/log/onlyoffice/documentserver/converter/err.log 2>/dev/null\"")
                .ConfigureAwait(false);

            static string Cap(string s, int max) => s.Length <= max ? s : "…(truncated)…\n" + s[^max..];

            return $"=== {container} docker logs (tail {tailLines}) ===\n{Cap(dockerLogs + dockerLogsErr, 6000)}\n" +
                   $"=== {container} docservice/converter logs (tail {tailLines}) ===\n{Cap(docservice, 8000)}";
        }
        catch (Exception ex)
        {
            return $"<CaptureOnlyOfficeLogsAsync threw: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    private static async Task<(string Stdout, string Stderr)> RunProcessAsync(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException($"Failed to start `{fileName} {arguments}`.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return (await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
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
        // sample/WopiHost/appsettings.json sets the FileSystemProvider RootPath to "../wopi-docs",
        // relative to the sample's directory => repo-root/sample/wopi-docs. Walk up from the test
        // binary until the repo root (the directory containing WOPI.slnx).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WOPI.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root (looking for WOPI.slnx) from " + AppContext.BaseDirectory + ".");
        }
        return Path.Combine(dir.FullName, "sample", "wopi-docs");
    }
}
