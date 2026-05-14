using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Visual Studio injects Microsoft.WebTools.ApiEndpointDiscovery into ASPNETCORE_HOSTINGSTARTUPASSEMBLIES
// when debugging ASP.NET Core (it's what populates the Endpoints Explorer / Connected Services
// panel). dotnet watch injects Microsoft.AspNetCore.Watch.BrowserRefresh the same way. Aspire
// spawns each child project as its own process and the include list is inherited, but the DLLs
// that satisfy those loads are delivered out-of-band in a way that doesn't reach the children,
// so each child throws FileNotFoundException at host startup. Non-fatal but loud, and only when
// launched from VS / dotnet watch.
//
// ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES is the documented opposite of the include list
// (https://learn.microsoft.com/aspnet/core/fundamentals/host/platform-specific-configuration):
// the host removes named assemblies from the discovered set before attempting to load. We use
// it here in preference to clearing the include list outright so any legitimate hosting-startup
// assembly an Aspire user (or a child project's own appsettings) adds keeps working — the fix
// is scoped to the two known dev-tooling offenders.
static IResourceBuilder<ProjectResource> ExcludeVsHostingStartups(IResourceBuilder<ProjectResource> p) =>
    p.WithEnvironment(
        "ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES",
        "Microsoft.WebTools.ApiEndpointDiscovery;Microsoft.AspNetCore.Watch.BrowserRefresh");

// WOPI backend.
//
// Port: pinned to 5050. Kestrel binds directly (isProxied: false) so the URL we hand other
// resources is the real host-side TCP socket. Direct binding matters for the Collabora dev
// loop specifically — Collabora-in-Docker reaches the backend via host.docker.internal:5050,
// and that has to be a port the host kernel is actually listening on (Aspire's reverse proxy
// doesn't help because the container can't see Aspire's internal allocator).
//
// Why 5050 and not 5000? On Windows, port 5000 sits inside the kernel-level excluded-port
// range that Hyper-V / WinNAT / WSL2 reserve for their NAT pool. Kestrel fails to bind with
// `SocketException 10013 (WSAEACCES) — An attempt was made to access a socket in a way
// forbidden by its access permissions`, even though `netstat` shows nothing listening. Check
// the local exclusions with `netsh int ipv4 show excludedportrange protocol=tcp` if 5050 is
// also unavailable on your machine; move to another free port and update Collabora's "domain"
// regex below in lockstep.
//
// Why pin the port at all rather than let Aspire allocate? In Aspire 13.x, WithHttpEndpoint
// with isProxied: false and no port silently hangs the AppHost during graph construction —
// the dashboard's web server never starts. Reproduced cleanly: removing port: here leaves
// startup stuck after "Application host directory is: …" with no further output. Pinning a
// port is the only working combo today. Downstream consumers still read this through a
// ReferenceExpression so the literal port only appears in two colocated places.
//
// launchProfileName: null bypasses sample/WopiHost/Properties/launchSettings.json so the
// AppHost is the single source of truth for backend configuration. We re-inject the one
// load-bearing setting — ASPNETCORE_ENVIRONMENT=Development — explicitly, because
// sample/WopiHost's Program.cs refuses to honour Wopi:Security:DisableProofValidation
// outside Development (and the Collabora block below flips that flag on).
var wopiHost = ExcludeVsHostingStartups(builder.AddProject<Projects.WopiHost>("wopihost", launchProfileName: null))
                      .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                      .WithHttpEndpoint(name: "wopihost-http", port: 5050, isProxied: false)
                      .WithUrlForEndpoint("wopihost-http", url =>
                      {
                          url.DisplayText = "Scalar (HTTP)";
                          url.Url = "/scalar";
                      });

// Reference expression to the backend's allocated port. Embedding this in interpolated
// string arguments to WithEnvironment / ReferenceExpression.Create:
//   (1) defers resolution to Aspire's endpoint-allocation phase, and
//   (2) auto-declares the dependency, so the consumer's resource doesn't try to compute
//       its env vars before the producer's endpoint has been allocated.
// (The older WithEnvironment(ctx => ... endpoint.Port) callback form looks the same but
//  does NOT carry the dependency, so it throws "endpoint not allocated" if the consumer
//  happens to resolve env vars first.)
var wopiBackendPort = wopiHost.GetEndpoint("wopihost-http").Property(EndpointProperty.Port);

// Optional: Azure Blob Storage backend via Azurite emulator. Opt-in via "AppHost:UseAzureStorage"=true
// so the default flow keeps using the file-system provider out of the box. When enabled, the Azurite
// resource is added and its connection string is forwarded to the WopiHost project as
// "ConnectionStrings__BlobStorage", which sample/WopiHost reads when configured for the Azure provider.
if (builder.Configuration.GetValue<bool>("AppHost:UseAzureStorage"))
{
    var storage = builder.AddAzureStorage("blob-storage")
                         .RunAsEmulator(emu => emu.WithLifetime(ContainerLifetime.Persistent));
    var blobs = storage.AddBlobs("BlobStorage");
    wopiHost.WithReference(blobs);
}

// Redis-backed distributed lock provider. Default ON when starting via Aspire — when the
// AppHost orchestrates the wopihost-web frontend, Aspire is already managing Docker resources
// and Redis is the realistic lock backend a real deployment would use; running the dev loop
// against the same provider catches divergences early. Opt out via "AppHost:UseRedisLocks"=false
// (e.g., on a contributor machine without Docker, or for the unit-test loop where
// MemoryLockProvider is sufficient).
//
// When enabled, an Aspire Redis container resource is added and the WopiHost backend is
// reconfigured to load WopiHost.RedisLockProvider with the Aspire-allocated connection string.
// WaitFor ensures the WOPI backend doesn't try to acquire a lock before Redis is accepting
// connections.
if (builder.Configuration.GetValue("AppHost:UseRedisLocks", defaultValue: true))
{
    var redis = builder.AddRedis("wopi-locks")
                       .WithLifetime(ContainerLifetime.Persistent);
    wopiHost
        // Switch the backend's lock-provider assembly name so AddLockProvider() dispatches to Redis.
        .WithEnvironment("Wopi__LockProviderAssemblyName", "WopiHost.RedisLockProvider")
        // sample/WopiHost binds Wopi:LockProvider:ConnectionString to WopiRedisLockProviderOptions;
        // route Aspire's connection-string reference through this key so the provider sees it
        // without needing a separate ConnectionStrings:wopi-locks fallback path.
        .WithEnvironment("Wopi__LockProvider__ConnectionString", redis.Resource.ConnectionStringExpression)
        .WaitFor(redis);
}

// Collabora-mode toggle. When enabled (see the dedicated block at the bottom of this file),
// the frontend's Wopi:HostUrl must use host.docker.internal so the URL it bakes into WopiSrc
// resolves from inside the Collabora container. In non-Collabora mode there's no in-Docker
// WOPI client (real OOS/M365 WOPI clients live outside the dev loop and configure their own
// URL), so localhost is fine for the dashboard.
var useCollabora = builder.Configuration.GetValue<bool>("AppHost:UseCollabora");
var wopiBackendHostForFrontends = useCollabora ? "host.docker.internal" : "localhost";

// Frontends: project references via WithReference give Aspire's service-discovery env vars
// (services__wopihost__http__0=...), but the existing frontend code reads Wopi:HostUrl directly
// from IConfiguration — so we ALSO inject Wopi__HostUrl pointing at the same backend port. That
// way the AppHost owns the URL end-to-end and the frontend's appsettings.json HostUrl entry is
// only the production default.
//
// ASPNETCORE_ENVIRONMENT=Development is re-injected here for the same reason as the backend:
// launchProfileName: null bypasses each frontend's Properties/launchSettings.json which was
// otherwise contributing the Development env var. Without it the frontends default to Production
// in the dev loop — detailed errors hidden, appsettings.Development.json ignored, etc.
var wopiHostWeb = ExcludeVsHostingStartups(builder.AddProject<Projects.WopiHost_Web>("wopihost-web", launchProfileName: null))
       .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
       .WithReference(wopiHost)
       .WithEnvironment("Wopi__HostUrl", ReferenceExpression.Create($"http://{wopiBackendHostForFrontends}:{wopiBackendPort}"))
       .WithHttpsEndpoint()
       .WithExternalHttpEndpoints();

// Validator: same wiring pattern as the Web frontend. The validator never runs against Collabora
// (it's a WOPI protocol checker), so localhost is the right reach.
ExcludeVsHostingStartups(builder.AddProject<Projects.WopiHost_Validator>("wopihost-validator", launchProfileName: null))
       .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
       .WithReference(wopiHost)
       .WithEnvironment("Wopi__HostUrl", ReferenceExpression.Create($"http://localhost:{wopiBackendPort}"))
       .WithHttpsEndpoint()
       .WithExternalHttpEndpoints();

// Optional: OIDC frontend sample. Opt-in via "AppHost:IncludeOidcSample"=true so newcomers don't
// need to register an IdP just to run the default flow. Requires the user to fill in Oidc:* config
// in sample/WopiHost.Web.Oidc/appsettings.Development.json (see that sample's README for setup).
if (builder.Configuration.GetValue<bool>("AppHost:IncludeOidcSample"))
{
    // OIDC requires HTTPS for cookie/redirect-URI sanity; Aspire picks the port. Note that
    // the OIDC sample's appsettings.Development.json must list whatever URL Aspire picks as
    // an allowed redirect URI on the IdP side, so dynamic allocation does mean re-registering
    // the redirect URI at the IdP after each port change. If that's painful in your setup,
    // pin via WithHttpsEndpoint(port: 6101).
    ExcludeVsHostingStartups(builder.AddProject<Projects.WopiHost_Web_Oidc>("wopihost-web-oidc", launchProfileName: null))
           .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
           .WithReference(wopiHost)
           .WithEnvironment("Wopi__HostUrl", ReferenceExpression.Create($"http://{wopiBackendHostForFrontends}:{wopiBackendPort}"))
           .WithHttpsEndpoint()
           .WithExternalHttpEndpoints();
}

// Optional: Collabora Online Development Edition (CODE) as a real WOPI client for end-to-end
// editing. Opt-in via "AppHost:UseCollabora"=true. CODE is free and Docker-distributable; it is
// a development substitute for Office Online Server / M365 for the Web (discovery output and
// supported features differ — do NOT treat a green Collabora run as M365 conformance).
//
// Wiring (port-independent for the project side — every project URL flows from wopiBackendPort):
//  - Browser reaches Collabora at http://localhost:9980 (Collabora's port stays pinned at 9980).
//  - Collabora (in Docker) reaches the WOPI backend via host.docker.internal:5050. On Linux
//    Docker, run with --add-host=host.docker.internal:host-gateway.
//  - "domain" is a regex (escape dots) of WOPI hosts Collabora is allowed to call back to; a
//    mismatch with the WopiSrc query param yields a silent 401.
//  - SSL is disabled for local dev only.
//
// Everything Collabora-specific lives in this one block: the container, the backend env vars
// that only make sense in Collabora mode, and the frontend env vars that point the iframe at
// Collabora's discovery. WaitFor on both backend and frontend blocks them until Collabora's
// /hosting/discovery returns 200 — otherwise Polly's Standard-Retry pipeline logs noisy
// "ResponseEnded" / 10s timeouts on the first page load while loolwsd is still binding.
if (useCollabora)
{
    // The "domain" regex stays a literal string rather than a ReferenceExpression interpolating
    // wopiBackendPort: container env vars that reference a project endpoint's Property(Port)
    // appear to wedge Aspire 13.x container startup in the "Starting" state indefinitely (the
    // container never reaches the health-check phase). Project-level env vars above use
    // ReferenceExpression fine; only the container case hangs. Since wopihost's port is pinned
    // to 5050 at the top of this file (Aspire requires it for isProxied:false), the two
    // literals are colocated and a future port change is a two-line edit.
    var collabora = builder.AddContainer("collabora", "collabora/code")
           .WithEnvironment("domain", "host\\.docker\\.internal:5050")
           .WithEnvironment("extra_params", "--o:ssl.enable=false --o:ssl.termination=false")
           .WithHttpEndpoint(targetPort: 9980, port: 9980, name: "collabora")
           .WithHttpHealthCheck("/hosting/discovery", endpointName: "collabora");

    // Backend: fetches /hosting/discovery from Collabora at startup. Collabora does not sign WOPI
    // callbacks with proof keys (those are an OOS / M365-for-the-Web feature) and emits no
    // <proof-key> element in discovery, so the default WopiProofValidator rejects every request
    // and CheckFileInfo 500s — the editor loads but the document never appears. The sample WOPI
    // host honours Wopi:Security:DisableProofValidation in Development to swap in a no-op
    // validator; refuse to run in non-Development if the flag is set.
    wopiHost.WithEnvironment("Wopi__ClientUrl", "http://localhost:9980")
            .WithEnvironment("Wopi__Security__DisableProofValidation", "true")
            .WaitFor(collabora);

    // Frontend: embeds Collabora; the iframe URL must come from Collabora's discovery, and the
    // WopiSrc it carries must resolve from inside the Collabora container (host.docker.internal,
    // already injected via Wopi__HostUrl above thanks to wopiBackendHostForFrontends). Collabora's
    // discovery XML emits a single <net-zone name="external-http"> only — defaulting to
    // ExternalHttps (as appsettings does for OOS/M365) silently filters every action and icon
    // out, so files render with the generic icon and edit/view buttons stay disabled.
    wopiHostWeb.WithEnvironment("Wopi__ClientUrl", "http://localhost:9980")
               .WithEnvironment("Wopi__Discovery__NetZone", "ExternalHttp")
               .WaitFor(collabora);
}

builder.Build().Run();
