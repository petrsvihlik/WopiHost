using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WopiHost.E2ETests.Fixtures;

/// <summary>
/// Shared scaffolding for the per-client e2e app fixtures. Boots the AppHost programmatically via
/// <c>DistributedApplicationTestingBuilder</c> with the lane's <c>AppHost:*</c> flags, waits for the
/// lane's resources to report Running, polls the WOPI client's readiness endpoint until it actually
/// serves, and exposes the lane's frontend URL for Playwright. Derived fixtures supply only the
/// lane specifics: flags, resource names, readiness semantics, and failure-diagnostic log capture.
/// </summary>
/// <remarks>
/// <para>
/// Docker is required. The fixture probes <c>docker info</c> at construction time and stores the
/// result in <see cref="IsDockerAvailable"/>; tests check this and skip when the engine is
/// unreachable. <see cref="InitializeAsync"/> does NOT throw on missing Docker — that would surface
/// as a fixture init failure (red) rather than a clean skip on contributor machines.
/// </para>
/// <para>
/// Each derived fixture disables the other lane and Redis so a suite spins up exactly one client
/// container; the memory lock provider is sufficient for the single-process happy-path scenarios
/// covered here. Cold-start cost (image pull + engine warmup) is paid once per collection.
/// </para>
/// </remarks>
public abstract class WopiAppFixtureBase : IAsyncLifetime
{
    private DistributedApplication? _app;

    /// <summary>Whether a usable Docker engine was reachable at fixture construction time.</summary>
    public bool IsDockerAvailable { get; } = DockerCheck.IsDockerAvailable();

    /// <summary>
    /// HTTPS URL of the lane's Web frontend after the application has started. Populated by
    /// <see cref="InitializeAsync"/>; reading before initialisation completes throws.
    /// </summary>
    public Uri WebFrontendUrl { get; private set; } = null!;

    /// <summary>
    /// Filesystem path to the writable <c>sample/wopi-docs</c> tree the sample WOPI host serves.
    /// Tests assert on file timestamps / sizes here to verify that PutFile callbacks from the
    /// WOPI client actually round-tripped back through the host to disk.
    /// </summary>
    public string WopiDocsPath { get; } = ResolveWopiDocsPath();

    /// <summary>Name of the WOPI-client container resource; also the name of its HTTP endpoint
    /// (the AppHost registers both under the same string, e.g. <c>collabora</c>).</summary>
    protected abstract string ClientResourceName { get; }

    /// <summary>Name of the lane's WOPI backend project resource.</summary>
    protected abstract string BackendResourceName { get; }

    /// <summary>Name of the lane's Web frontend project resource.</summary>
    protected abstract string FrontendResourceName { get; }

    /// <summary>
    /// <c>AppHost:*</c> flags fed through the <c>configureBuilder</c> seam. CreateAsync runs the
    /// AppHost's Program.cs (resource-graph discovery) BEFORE returning, so post-CreateAsync
    /// configuration mutations are too late to flip the resource graph — this callback is the only
    /// seam that runs before the AppHost reads <c>builder.Configuration.GetValue&lt;bool&gt;(...)</c>.
    /// </summary>
    protected abstract IReadOnlyDictionary<string, string?> AppHostFlags { get; }

    /// <summary>Path on the client container polled until the client actually serves
    /// (resource state Running only means the orchestrator issued the start command).</summary>
    protected abstract string ReadinessProbePath { get; }

    /// <summary>Overall startup budget: image pull on a clean cache + engine warmup dominate.</summary>
    protected abstract TimeSpan StartupTimeout { get; }

    /// <summary>Budget for the readiness-probe loop once resources report Running.</summary>
    protected abstract TimeSpan ReadinessTimeout { get; }

    /// <summary>
    /// Captures the client container's recent logs for failure diagnostics, so a failing test can
    /// surface the client's own rejection reasons (WOPI callback errors, auth denials) that aren't
    /// visible from the browser side.
    /// </summary>
    public abstract Task<string> CaptureClientLogsAsync();

    /// <summary>
    /// Decides whether a readiness-probe response means the client is ready to serve. Default: any
    /// 2xx. Override when readiness needs body inspection (e.g. ONLYOFFICE's <c>/healthcheck</c>
    /// returns 200 with a literal <c>true</c>/<c>false</c> body).
    /// </summary>
    protected virtual Task<bool> IsReadyResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        => Task.FromResult(response.IsSuccessStatusCode);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (!IsDockerAvailable)
        {
            // Start nothing — tests inspect IsDockerAvailable and skip.
            return;
        }

        var appBuilder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.WopiHost_AppHost>([], (_, settings) =>
            {
                settings.Configuration ??= new ConfigurationManager();
                settings.Configuration.AddInMemoryCollection(AppHostFlags);
            })
            .ConfigureAwait(false);

        // Fail fast if the flags didn't propagate (the client container won't be in the graph)
        // rather than wait through the timeouts below.
        var resourceNames = appBuilder.Resources.Select(r => r.Name).ToList();
        if (!resourceNames.Contains(ClientResourceName))
        {
            throw new InvalidOperationException(
                $"'{ClientResourceName}' resource not found in the AppHost graph after CreateAsync. " +
                $"Resources: [{string.Join(", ", resourceNames)}]. The configureBuilder callback didn't " +
                "apply the in-memory configuration source before the AppHost's Program.cs ran.");
        }

        _app = await appBuilder.BuildAsync().ConfigureAwait(false);
        await _app.StartAsync().ConfigureAwait(false);

        // Wait for each resource to be reported Running by Aspire. Running != accepting traffic —
        // the explicit HTTP poll below backstops this to confirm the client is actually serving.
        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(StartupTimeout);
        await notifications.WaitForResourceAsync(ClientResourceName, KnownResourceStates.Running, cts.Token).ConfigureAwait(false);
        await notifications.WaitForResourceAsync(BackendResourceName, KnownResourceStates.Running, cts.Token).ConfigureAwait(false);
        await notifications.WaitForResourceAsync(FrontendResourceName, KnownResourceStates.Running, cts.Token).ConfigureAwait(false);

        // Belt-and-braces probe against the client's readiness endpoint. Use the Aspire-discovered
        // endpoint, NOT the host port the AppHost requests: under DistributedApplicationTestingBuilder,
        // DCP can remap the host port even when the AppHost asks for a specific one.
        var clientEndpoint = _app.GetEndpoint(ClientResourceName, ClientResourceName);
        var probeUrl = new Uri(clientEndpoint, ReadinessProbePath);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow + ReadinessTimeout;
        var ready = false;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync(probeUrl, cts.Token).ConfigureAwait(false);
                if (await IsReadyResponseAsync(response, cts.Token).ConfigureAwait(false))
                {
                    ready = true;
                    break;
                }
            }
            catch (HttpRequestException)
            {
                // Connection refused / DNS — the client is not yet serving. Keep polling.
            }
            catch (TaskCanceledException) when (!cts.Token.IsCancellationRequested)
            {
                // Per-request HttpClient.Timeout elapsed (client not yet bound). Distinct from
                // cts.Token cancellation — that one means the overall deadline was hit.
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token).ConfigureAwait(false);
        }
        if (!ready)
        {
            throw new InvalidOperationException(
                $"{ClientResourceName}'s {ReadinessProbePath} did not report ready within {ReadinessTimeout.TotalMinutes:0.#} minutes.\n" +
                $"Aspire-discovered endpoint: {clientEndpoint}\n" +
                $"Probed URL:                 {probeUrl}\n" +
                $"Containers visible to docker:\n{await CaptureDockerPsAsync().ConfigureAwait(false)}\n" +
                $"If `docker ps -a` shows no {ClientResourceName}-* row, DCP isn't actually starting containers " +
                "in this test-mode run. If it shows a row with status 'Exited', inspect its logs.");
        }

        WebFrontendUrl = new Uri(_app.GetEndpoint(FrontendResourceName, "https").ToString());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Runs a process to completion and returns both output streams. Used by the
    /// docker-based diagnostics helpers in derived fixtures.</summary>
    protected static async Task<(string Stdout, string Stderr)> RunProcessAsync(string fileName, string arguments)
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

    private static string ResolveWopiDocsPath()
    {
        // sample/WopiHost/appsettings.json sets the FileSystemProvider RootPath to "../wopi-docs",
        // relative to the sample's directory => repo-root/sample/wopi-docs. Walk up from the test
        // binary until the repo root (the directory containing WOPI.slnx). Robust to the artifacts/
        // layout (UseArtifactsOutput) which puts test dlls several levels below.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Join(dir.FullName, "WOPI.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root (looking for WOPI.slnx) from " + AppContext.BaseDirectory + ".");
        }
        return Path.Join(dir.FullName, "sample", "wopi-docs");
    }
}
