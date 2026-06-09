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
// the host removes named assemblies from the discovered set before attempting to load. Excluding
// the two known dev-tooling offenders (rather than clearing the include list outright) keeps any
// legitimate hosting-startup assembly an Aspire user or child project adds working.
static IResourceBuilder<ProjectResource> ExcludeVsHostingStartups(IResourceBuilder<ProjectResource> p) =>
    p.WithEnvironment(
        "ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES",
        "Microsoft.WebTools.ApiEndpointDiscovery;Microsoft.AspNetCore.Watch.BrowserRefresh");

// WOPI-client toggles. Each enabled real client gets its own self-contained (backend, frontend)
// lane. The backend consults the client only for proof keys (WopiProofValidator is the sole
// discovery consumer in WopiHost.Core), so a backend per client lets each lane run its own
// proof-validation mode independently (both default to proof off in dev — Collabora can't sign
// callbacks at all, and ONLYOFFICE's signatures are rejected by WopiProofValidator; see the
// onlyOfficeProofValidation note below). One backend can't hold two clients' proof keys, so
// per-lane proof config is what requires a backend per client.
var useCollabora = builder.Configuration.GetValue("AppHost:UseCollabora", defaultValue: true);
// Defaults ON like Collabora so the orchestrated dev loop brings up both editors for side-by-side
// testing. Heavier than the rest of the default set — the onlyoffice/documentserver image is ~4.3 GB
// and bundles its own Postgres/RabbitMQ — so a contributor without the disk/RAM for it (or without
// Docker) opts out with AppHost:UseOnlyOffice=false.
var useOnlyOffice = builder.Configuration.GetValue("AppHost:UseOnlyOffice", defaultValue: true);

// ONLYOFFICE advertises a <proof-key> in its discovery, but empirically its WOPI GetFile callback
// is rejected by the host's WopiProofValidator: the editor shell loads, then the document download
// 500s and ONLYOFFICE shows "Download failed". So the lane defaults to proof OFF to be functional
// out of the box (same as Collabora). The separate backend still earns its keep — it keeps this
// toggle per-lane, so flipping AppHost:OnlyOfficeProofValidation=true drives the real
// WopiProofValidator path against ONLYOFFICE (to investigate why its signature is rejected — does it
// send X-WOPI-Proof at all?) without disturbing the working Collabora lane.
var onlyOfficeProofValidation = builder.Configuration.GetValue("AppHost:OnlyOfficeProofValidation", defaultValue: false);

// Registers a WOPI backend pinned to a host-reachable TCP port. Kestrel binds directly
// (isProxied: false) so the URL handed to other resources — and to Docker WOPI clients reaching
// back via host.docker.internal:<port> — is the real host-side socket; Aspire's reverse proxy
// doesn't help because the client container can't see Aspire's internal allocator.
//
// Why pin the port rather than let Aspire allocate? In Aspire 13.x, WithHttpEndpoint with
// isProxied: false and no port silently hangs the AppHost during graph construction — startup
// sticks after "Application host directory is: …" and the dashboard never starts. Downstream
// consumers read the port through a ReferenceExpression so the literal only appears in the lane
// wiring below.
//
// Why 5050+ and not 5000? On Windows, port 5000 sits inside the kernel-level excluded-port range
// that Hyper-V / WinNAT / WSL2 reserve for their NAT pool; Kestrel fails to bind (SocketException
// 10013 / WSAEACCES) even though netstat shows nothing listening. Check local exclusions with
// `netsh int ipv4 show excludedportrange protocol=tcp` and move both the port and the matching
// client's allowed-host config in lockstep if 5050/5051 are unavailable.
//
// launchProfileName: null bypasses sample/WopiHost/Properties/launchSettings.json so the AppHost
// is the single source of truth for backend config. ASPNETCORE_ENVIRONMENT=Development is injected
// explicitly because sample/WopiHost refuses to honour DisableProofValidation outside Development.
//
// hasDockerClient drives ASPNETCORE_URLS=http://0.0.0.0:<port> — the load-bearing bind for any
// Docker setup using a bridge network (Linux Docker Engine — GitHub Actions, native Linux). Without
// it Kestrel binds localhost:<port> and host.docker.internal from inside the container resolves to
// the docker0 bridge host IP with nothing listening → ECONNREFUSED on every WOPI callback (surfaced
// misleadingly as a websocketunauthorized error in the iframe). On Docker Desktop (Mac/Windows) the
// explicit bind is a no-op, so it's safe to set whenever a client exists.
IResourceBuilder<ProjectResource> AddBackend(string name, int port, bool hasDockerClient, bool disableProofValidation)
{
    var backend = ExcludeVsHostingStartups(builder.AddProject<Projects.WopiHost>(name, launchProfileName: null))
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithHttpEndpoint(name: $"{name}-http", port: port, isProxied: false)
        .WithUrlForEndpoint($"{name}-http", url =>
        {
            url.DisplayText = "Scalar (HTTP)";
            url.Url = "/scalar";
        });

    if (hasDockerClient)
    {
        // Hoisted to a string local so overload resolution picks WithEnvironment(string, string).
        // An inline interpolated literal binds to the ReferenceExpression handler overload instead,
        // which rejects the int hole.
        var bindAllUrls = $"http://0.0.0.0:{port}";
        backend.WithEnvironment("ASPNETCORE_URLS", bindAllUrls);
    }

    if (disableProofValidation)
    {
        backend.WithEnvironment("Wopi__Security__DisableProofValidation", "true");
    }

    return backend;
}

// Registers a WopiHost.Web frontend bound to a backend. Same project, same compiled code for every
// lane — only the injected backend URL (and, in each client block below, Wopi__ClientUrl + NetZone)
// differ, which is the whole point: a second editor with zero project duplication.
//
// WithReference gives Aspire's service-discovery env vars, but the frontend reads Wopi:HostUrl
// directly from IConfiguration — so Wopi__HostUrl is ALSO injected, pointing at the same backend
// port. backendReachHost must be host.docker.internal whenever the lane's client runs in Docker, so
// the WopiSrc the frontend bakes resolves from inside the client container; with no in-Docker client
// localhost is fine. ASPNETCORE_ENVIRONMENT=Development is re-injected for the same
// launchProfileName: null reason as the backend.
IResourceBuilder<ProjectResource> AddWebFrontend(string name, IResourceBuilder<ProjectResource> backend, string backendReachHost)
{
    var backendPort = backend.GetEndpoint($"{backend.Resource.Name}-http").Property(EndpointProperty.Port);
    return ExcludeVsHostingStartups(builder.AddProject<Projects.WopiHost_Web>(name, launchProfileName: null))
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithReference(backend)
        .WithEnvironment("Wopi__HostUrl", ReferenceExpression.Create($"http://{backendReachHost}:{backendPort}"))
        .WithHttpsEndpoint()
        .WithExternalHttpEndpoints();
}

// Every backend lane accumulates here so the shared lock + storage backends below wire to all of
// them in one pass: a single Redis instance coordinates locks across every backend (a lock taken
// while editing in one client is visible to the other), and a single Azurite blob container backs
// the same file set in each.
var backends = new List<IResourceBuilder<ProjectResource>>();

// ---- Collabora / default lane -------------------------------------------------------------
// Always present. Runs as the Collabora lane when UseCollabora (proof off, reachable from inside the
// container), otherwise a plain lane pointed at the appsettings default client (M365) with proof
// left on and localhost-only reach — there's no in-Docker WOPI client to call back.
//
// Resource names carry a "-collabora" suffix when this is the Collabora lane, mirroring the
// "-onlyoffice" lane so the dashboard reads symmetrically (wopihost-collabora / wopihost-web-collabora
// next to wopihost-onlyoffice / wopihost-web-onlyoffice). Without Collabora there's no client, so the
// plain wopihost / wopihost-web names are used.
var primaryReachHost = useCollabora ? "host.docker.internal" : "localhost";
var primaryBackendName = useCollabora ? "wopihost-collabora" : "wopihost";
var primaryWebName = useCollabora ? "wopihost-web-collabora" : "wopihost-web";
var primaryBackend = AddBackend(primaryBackendName, port: 5050, hasDockerClient: useCollabora, disableProofValidation: useCollabora);
backends.Add(primaryBackend);
var primaryBackendPort = primaryBackend.GetEndpoint($"{primaryBackendName}-http").Property(EndpointProperty.Port);
var wopiHostWeb = AddWebFrontend(primaryWebName, primaryBackend, primaryReachHost);

// Validator: same wiring as the Web frontend but always localhost — it's a WOPI protocol checker
// that never runs against an in-Docker client.
ExcludeVsHostingStartups(builder.AddProject<Projects.WopiHost_Validator>("wopihost-validator", launchProfileName: null))
       .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
       .WithReference(primaryBackend)
       .WithEnvironment("Wopi__HostUrl", ReferenceExpression.Create($"http://localhost:{primaryBackendPort}"))
       .WithHttpsEndpoint()
       .WithExternalHttpEndpoints();

// ---- ONLYOFFICE lane ----------------------------------------------------------------------
// Backend + frontend are created up front (so the backend joins the shared-infra pass below); the
// container and Wopi__ClientUrl wiring follow in the dedicated block near the bottom.
IResourceBuilder<ProjectResource>? onlyOfficeBackend = null;
IResourceBuilder<ProjectResource>? onlyOfficeWeb = null;
if (useOnlyOffice)
{
    onlyOfficeBackend = AddBackend("wopihost-onlyoffice", port: 5051, hasDockerClient: true, disableProofValidation: !onlyOfficeProofValidation);
    backends.Add(onlyOfficeBackend);
    onlyOfficeWeb = AddWebFrontend("wopihost-web-onlyoffice", onlyOfficeBackend, "host.docker.internal");
}

// ---- shared lock + storage backends -------------------------------------------------------

// Redis-backed distributed lock provider. Default ON when starting via Aspire — Aspire is already
// managing Docker resources and Redis is the realistic lock backend a real deployment would use, so
// running the dev loop against it catches divergences early. With more than one backend it's also
// what makes their locks coordinate: every backend points at this single instance, so a lock held
// while editing a file in one client blocks the other. Opt out via "AppHost:UseRedisLocks"=false
// (a contributor machine without Docker, or the MemoryLockProvider unit-test loop) — note that with
// the in-memory provider each backend's locks are per-process and will NOT coordinate.
//
// Lifetime defaults to Session (Aspire's default) so the container is torn down on AppHost shutdown.
// Persistent lifetime accumulates orphaned containers across runs: Aspire's persistent-resource
// identity is fingerprinted from the resource config (including the auto-generated REDIS_PASSWORD),
// so a fresh password each session produces a fresh fingerprint and a fresh container while the
// previous one lingers as `Exited`. Lock state is short-lived by design (30-min WOPI spec expiry),
// so there's no data-survival need that would justify pinning the password to stabilise it.
if (builder.Configuration.GetValue("AppHost:UseRedisLocks", defaultValue: true))
{
    var redis = builder.AddRedis("wopi-locks");
    foreach (var backend in backends)
    {
        backend
            // Flip the sample's lock-provider discriminator so AddSampleLockProvider() dispatches to
            // Redis. Sample:LockProvider lives in the sample's appsettings (not WopiHost.Core's
            // public options surface) — see sample/WopiHost/ServiceCollectionExtensions.cs.
            .WithEnvironment("Sample__LockProvider", "Redis")
            // sample/WopiHost binds Wopi:LockProvider:ConnectionString to WopiRedisLockProviderOptions;
            // route Aspire's connection-string reference through this key so the provider sees it
            // without needing a separate ConnectionStrings:wopi-locks fallback path.
            .WithEnvironment("Wopi__LockProvider__ConnectionString", redis.Resource.ConnectionStringExpression)
            .WaitFor(redis);
    }
}

// Optional: Azure Blob Storage backend via Azurite emulator. Opt-in via "AppHost:UseAzureStorage"=true
// so the default flow keeps using the file-system provider out of the box. When enabled, the Azurite
// resource is added and its connection string is forwarded to every backend as
// "ConnectionStrings__BlobStorage", which sample/WopiHost reads when configured for the Azure provider.
if (builder.Configuration.GetValue("AppHost:UseAzureStorage", defaultValue: false))
{
    var storage = builder.AddAzureStorage("blob-storage")
                         .RunAsEmulator(emu => emu.WithLifetime(ContainerLifetime.Persistent));
    var blobs = storage.AddBlobs("BlobStorage");
    foreach (var backend in backends)
    {
        backend.WithReference(blobs);
    }
}

// Optional: OIDC frontend sample. Opt-in via "AppHost:IncludeOidcSample"=true so newcomers don't
// need to register an IdP just to run the default flow. Shares the default lane's backend. Requires
// the user to fill in Oidc:* config in sample/WopiHost.Web.Oidc/appsettings.Development.json (see
// that sample's README for setup).
if (builder.Configuration.GetValue("AppHost:IncludeOidcSample", defaultValue: false))
{
    // OIDC requires HTTPS for cookie/redirect-URI sanity; Aspire picks the port. Note that the OIDC
    // sample's appsettings.Development.json must list whatever URL Aspire picks as an allowed redirect
    // URI on the IdP side, so dynamic allocation does mean re-registering the redirect URI at the IdP
    // after each port change. If that's painful in your setup, pin via WithHttpsEndpoint(port: 6101).
    ExcludeVsHostingStartups(builder.AddProject<Projects.WopiHost_Web_Oidc>("wopihost-web-oidc", launchProfileName: null))
           .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
           .WithReference(primaryBackend)
           .WithEnvironment("Wopi__HostUrl", ReferenceExpression.Create($"http://{primaryReachHost}:{primaryBackendPort}"))
           .WithHttpsEndpoint()
           .WithExternalHttpEndpoints();
}

// ---- Collabora container -------------------------------------------------------------------
// Collabora Online Development Edition (CODE) as a real WOPI client for end-to-end editing. CODE is
// free and Docker-distributable; it is a development substitute for Office Online Server / M365 for
// the Web (discovery output and supported features differ — do NOT treat a green Collabora run as
// M365 conformance). The default-lane backend's proof-off + 0.0.0.0 bind are set by AddBackend above
// (hasDockerClient/disableProofValidation both follow useCollabora); everything else lives here.
//
//  - Browser reaches Collabora at http://localhost:9980 (Collabora's port stays pinned at 9980).
//  - Collabora (in Docker) reaches the WOPI backend via host.docker.internal:5050. On Linux Docker,
//    run with --add-host=host.docker.internal:host-gateway.
//  - "domain" is a regex (escape dots) of WOPI hosts Collabora is allowed to call back to; a
//    mismatch with the WopiSrc query param yields a silent 401.
//  - SSL is disabled for local dev only.
//
// WaitFor on both backend and frontend blocks them until Collabora's /hosting/discovery returns 200 —
// otherwise Polly's Standard-Retry pipeline logs noisy "ResponseEnded" / 10s timeouts on first load
// while loolwsd is still binding.
if (useCollabora)
{
    var collabora = builder.AddContainer("collabora", "collabora/code")
           // host.docker.internal needs an explicit --add-host=host.docker.internal:host-gateway on
           // Linux Docker Engine — Docker Desktop on Mac/Windows wires it implicitly, but the Linux
           // daemon doesn't. Without this, Collabora's WOPI callbacks from inside the container can't
           // reach the backend at host.docker.internal:5050: the editor shell loads (Collabora
           // doesn't need WOPI for that), but CheckFileInfo + GetFile time out and the canvas stays
           // blank. On Docker Desktop the explicit entry is a no-op, so it's safe unconditionally.
           .WithContainerRuntimeArgs("--add-host", "host.docker.internal:host-gateway")
           // WOPI hosts Collabora is allowed to call back to. coolwsd matches this regex against the
           // WopiSrc host with std::regex_match (full-string match), and the host it sees carries the
           // port — "host.docker.internal:5050". A bare "host\.docker\.internal" 401s
           // ("websocketunauthorized" / "Unauthorized WOPI host") because the ":5050" suffix leaves
           // the full match unsatisfied. Anchoring the known dev host as a prefix and absorbing the
           // port (or any path) with a trailing ".*" keeps the pattern scoped to host.docker.internal
           // instead of waving through every hostname. The port is a literal rather than a
           // ReferenceExpression interpolating the backend endpoint: container env vars that reference
           // a project endpoint's Property(Port) wedge Aspire 13.x container startup in "Starting"
           // indefinitely. Since the backend port is pinned to 5050, the two literals are colocated.
           .WithEnvironment("domain", @"host\.docker\.internal.*")
           // --o:logging.level=trace so the container log shows the actual host string being matched
           // (and why a match fails). CI captures container logs on failure, so a failing run surfaces
           // coolwsd's own diagnosis. Do NOT also set --o:logging.protocol=true: it traces HTTP bodies
           // including the ~600-line /hosting/discovery response, pushing the WebSocket-auth log lines
           // off `docker logs --tail`. Trace level alone surfaces the matching attempt.
           .WithEnvironment("extra_params", "--o:ssl.enable=false --o:ssl.termination=false --o:logging.level=trace")
           .WithHttpEndpoint(targetPort: 9980, port: 9980, name: "collabora")
           .WithHttpHealthCheck("/hosting/discovery", endpointName: "collabora");

    // Project-side env vars consume Collabora's endpoint via ReferenceExpression — NOT a literal
    // "http://localhost:9980". The literal happens to work when launching normally (Aspire honors the
    // `port: 9980` request, so DCP binds 9980 → 9980 on the host), but under Aspire.Hosting.Testing
    // DCP allocates a different host port even though the AppHost asks for 9980. With a reference, the
    // URL the projects see tracks whatever Aspire actually allocated, in both normal and test runs.
    // (This is the OPPOSITE direction of the `domain` footgun above: referencing a container endpoint
    // from a project env var works fine.)
    var collaboraUrl = collabora.GetEndpoint("collabora");

    // Backend fetches /hosting/discovery from Collabora at startup. Collabora doesn't sign WOPI
    // callbacks with proof keys and emits no <proof-key> in discovery, so proof validation must be
    // disabled (handled by the lane's disableProofValidation: useCollabora above) or every request
    // 500s — the editor loads but the document never appears.
    primaryBackend.WithEnvironment("Wopi__ClientUrl", collaboraUrl).WaitFor(collabora);

    // Frontend embeds Collabora; the iframe URL comes from Collabora's discovery and the WopiSrc it
    // carries resolves from inside the container (host.docker.internal, already injected via
    // Wopi__HostUrl thanks to primaryReachHost). Collabora's discovery XML emits a single
    // <net-zone name="external-http"> only — defaulting to ExternalHttps (as appsettings does for
    // OOS/M365) silently filters every action and icon out, so files render with the generic icon and
    // edit/view buttons stay disabled.
    wopiHostWeb.WithEnvironment("Wopi__ClientUrl", collaboraUrl)
               .WithEnvironment("Wopi__Discovery__NetZone", "ExternalHttp")
               .WaitFor(collabora);
}

// ---- ONLYOFFICE container ------------------------------------------------------------------
// ONLYOFFICE Document Server as a second real WOPI client, on its own backend so its proof-validation
// mode is independent of the Collabora lane. Heavyweight image — it bundles its own PostgreSQL /
// RabbitMQ and is markedly slower to start than Collabora; the health check + WaitFor below hold
// dependents until /hosting/discovery answers. Opt-in via "AppHost:UseOnlyOffice"=true.
if (useOnlyOffice)
{
    var onlyoffice = builder.AddContainer("onlyoffice", "onlyoffice/documentserver")
           // Same host-gateway reasoning as Collabora — ONLYOFFICE's WOPI callbacks reach the backend
           // at host.docker.internal:5051.
           .WithContainerRuntimeArgs("--add-host", "host.docker.internal:host-gateway")
           // WOPI handlers ship off by default; WOPI_ENABLED turns them (and /hosting/discovery) on.
           .WithEnvironment("WOPI_ENABLED", "true")
           // Disables ONLYOFFICE's own document-API JWT for the dev loop. That JWT secures ONLYOFFICE's
           // native API and is independent of WOPI proof keys, which secure the host↔client callbacks.
           .WithEnvironment("JWT_ENABLED", "false")
           // ONLYOFFICE serves HTTP on container port 80. Unlike Collabora's `domain` regex, ONLYOFFICE
           // trusts all callback hosts by default, so no allowed-host config is needed for local dev.
           .WithHttpEndpoint(targetPort: 80, port: 9981, name: "onlyoffice")
           .WithHttpHealthCheck("/hosting/discovery", endpointName: "onlyoffice");

    var onlyofficeUrl = onlyoffice.GetEndpoint("onlyoffice");

    // Backend fetches /hosting/discovery from ONLYOFFICE — to build editor URLs, and for proof keys
    // when the lane is flipped to proof ON (off by default; see onlyOfficeProofValidation).
    onlyOfficeBackend!.WithEnvironment("Wopi__ClientUrl", onlyofficeUrl).WaitFor(onlyoffice);

    // Frontend embeds ONLYOFFICE; iframe URL + WopiSrc come from its discovery. NetZone must match the
    // net-zone ONLYOFFICE advertises — start with ExternalHttp (plain-HTTP dev, same as Collabora) and
    // adjust if its discovery names a different zone.
    onlyOfficeWeb!.WithEnvironment("Wopi__ClientUrl", onlyofficeUrl)
                  .WithEnvironment("Wopi__Discovery__NetZone", "ExternalHttp")
                  .WaitFor(onlyoffice);
}

builder.Build().Run();
