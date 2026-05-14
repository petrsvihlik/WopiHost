using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
/// unreachable. We do NOT throw from <see cref="InitializeAsync"/> on missing Docker — that
/// would surface as a hard failure rather than a skip on contributor machines.
/// </para>
/// <para>
/// Redis is explicitly disabled (<c>AppHost:UseRedisLocks=false</c>) so the e2e test cycle
/// only depends on a single container (Collabora) rather than two. The memory lock provider
/// is sufficient for the single-process happy-path scenarios we test here.
/// </para>
/// </remarks>
public sealed class CollaboraAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public bool IsDockerAvailable { get; } = DockerCheck.IsDockerAvailable();

    /// <summary>
    /// HTTPS URL of the <c>wopihost-web</c> resource after the application has been started.
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
            // Don't start anything — tests will inspect IsDockerAvailable and skip. Throwing
            // here would surface as a fixture init failure (red) rather than a clean skip.
            return;
        }

        // Feed AppHost:UseCollabora / AppHost:UseRedisLocks via HostApplicationBuilderSettings.
        // DistributedApplicationTestingBuilder.CreateAsync runs the AppHost's Program.cs
        // (resource-graph discovery) BEFORE returning, so post-CreateAsync configuration
        // mutations on the returned builder are too late to flip the resource graph. The
        // configureBuilder overload is the documented seam — its callback fires before the
        // AppHost reads `builder.Configuration.GetValue<bool>(...)`. Command-line --args were
        // tried first and silently no-op'd (the resource graph kept Redis on, Collabora off).
        var appBuilder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.WopiHost_AppHost>([], (DistributedApplicationOptions _, HostApplicationBuilderSettings settings) =>
            {
                settings.Configuration ??= new ConfigurationManager();
                settings.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AppHost:UseCollabora"] = "true",
                    ["AppHost:UseRedisLocks"] = "false",
                });
            })
            .ConfigureAwait(false);

        // Sanity check: if the AppHost:UseCollabora flag didn't propagate (it's been a recurring
        // gotcha — args and `appBuilder.Configuration[..]="true"` *after* CreateAsync silently
        // no-op because the AppHost has already read the value during entry-point invocation;
        // the configureBuilder callback is the only seam that runs *before* that read), the
        // Collabora container won't be in the resource graph. Fail fast here with a clear
        // message rather than wait through the timeouts below.
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
        // WaitFors the backend internally), so we wait on the chain end.
        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)); // generous for first-run image pull

        // Wait for each resource to be reported in a Running state by Aspire's resource
        // notification service. Note that Running != accepting traffic — Aspire's Running
        // signal only means the orchestrator has issued the start command. We backstop with
        // an explicit HTTP poll against Collabora's /hosting/discovery below to confirm the
        // container is actually serving.
        await notifications.WaitForResourceAsync("collabora", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);
        await notifications.WaitForResourceAsync("wopihost", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);
        await notifications.WaitForResourceAsync("wopihost-web", KnownResourceStates.Running, cts.Token).ConfigureAwait(false);

        // Belt-and-braces probe: poll Collabora's /hosting/discovery directly until it serves
        // a 200, or fail the fixture with a diagnostic message that beats waiting through the
        // downstream Playwright timeout. Pinned to localhost:9980 — the AppHost binds the
        // Collabora endpoint there.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var collaboraDeadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTime.UtcNow < collaboraDeadline)
        {
            try
            {
                using var response = await http.GetAsync("http://localhost:9980/hosting/discovery", cts.Token).ConfigureAwait(false);
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
                // from cts.Token cancellation — that one means we hit the overall deadline.
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token).ConfigureAwait(false);
        }
        if (DateTime.UtcNow >= collaboraDeadline)
        {
            throw new InvalidOperationException(
                "Collabora's /hosting/discovery did not respond 200 within 2 minutes. The container is in the resource graph " +
                "and Aspire reported it as Running, but the HTTP endpoint never accepted traffic. Most likely causes: " +
                "Docker Desktop is not in Linux-container mode; port 9980 is already bound on the host; or the Collabora image " +
                "failed to start (check `docker ps -a` for an exited collabora-* container).");
        }

        WebFrontendUrl = new Uri(_app.GetEndpoint("wopihost-web", "https").ToString());
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
        // We walk up from the test binary location until we hit the repo root (the directory
        // containing WOPI.slnx) and then point at sample/wopi-docs from there. Robust to the
        // artifacts/ layout (UseArtifactsOutput) which puts test dlls several levels below.
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
