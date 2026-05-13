using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// WOPI backend.
//
// Port allocation: Aspire picks the port from the OS's free pool; Kestrel binds directly
// (isProxied: false) so the URL we hand other resources is the real host-side TCP socket.
// Direct binding matters for the Collabora dev loop specifically — Collabora-in-Docker
// reaches the backend via host.docker.internal:<port>, and that "<port>" has to be a port
// the host kernel is actually listening on (Aspire's reverse proxy doesn't help because
// the container can't see Aspire's internal allocator).
//
// launchProfileName: null tells Aspire to ignore sample/WopiHost/Properties/launchSettings.json
// — the AppHost is the sole owner of port + URL configuration. Without this, launchSettings
// silently contributes endpoint hints that drift from what WithEndpoint declares.
var wopiHost = builder.AddProject<Projects.WopiHost>("wopihost", launchProfileName: null)
                      .WithHttpEndpoint(name: "wopihost-http", isProxied: false)
                      .WithUrlForEndpoint("wopihost-http", url =>
                      {
                          url.DisplayText = "Scalar (HTTP)";
                          url.Url = "/scalar";
                      });

// Resolved at runtime by Aspire's endpoint allocator — readable inside any WithEnvironment
// callback once the dependency graph has reached the env-var-resolution phase.
var wopiBackendEndpoint = wopiHost.GetEndpoint("wopihost-http");

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

// Optional: Collabora Online Development Edition (CODE) as a real WOPI client for end-to-end
// editing. Opt-in via "AppHost:UseCollabora"=true. CODE is free and Docker-distributable; it is
// a development substitute for Office Online Server / M365 for the Web (discovery output and
// supported features differ — do NOT treat a green Collabora run as M365 conformance).
//
// Wiring (now dynamic — every URL flows from wopiBackendEndpoint.Port):
//  - Browser reaches Collabora at http://localhost:9980 (Collabora's port stays pinned at 9980).
//  - Collabora (in Docker) reaches the WOPI backend via host.docker.internal:<dynamic-port>,
//    where <dynamic-port> = the Aspire-allocated port for the wopihost project. On Linux Docker,
//    run with --add-host=host.docker.internal:host-gateway.
//  - "domain" is a regex (escape dots) of WOPI hosts Collabora is allowed to call back to; a
//    mismatch with the WopiSrc query param yields a silent 401. We compose it dynamically from
//    the backend port so port changes don't desync the allow-list.
//  - SSL is disabled for local dev only.
var useCollabora = builder.Configuration.GetValue<bool>("AppHost:UseCollabora");
IResourceBuilder<ContainerResource>? collabora = null;
if (useCollabora)
{
    // Health check on /hosting/discovery: the loolwsd process inside the container takes a few
    // seconds to bind to 9980 even after the TCP listener accepts. Without this, dependents
    // would race ahead and Polly's Standard-Retry pipeline logs "ResponseEnded" / 10s timeouts
    // on the first page load. WaitFor below blocks dependents until this returns 200.
    collabora = builder.AddContainer("collabora", "collabora/code")
           .WithEnvironment(ctx =>
           {
               ctx.EnvironmentVariables["domain"] = $"host\\.docker\\.internal:{wopiBackendEndpoint.Port}";
           })
           .WithEnvironment("extra_params", "--o:ssl.enable=false --o:ssl.termination=false")
           .WithHttpEndpoint(targetPort: 9980, port: 9980, name: "collabora")
           .WithHttpHealthCheck("/hosting/discovery", endpointName: "collabora");

    // Backend fetches /hosting/discovery from Collabora at startup. Collabora does not sign WOPI
    // callbacks with proof keys (those are an OOS / M365-for-the-Web feature) and emits no
    // <proof-key> element in discovery, so the default WopiProofValidator rejects every request
    // and CheckFileInfo 500s — the editor loads but the document never appears. The sample WOPI
    // host honours Wopi:Security:DisableProofValidation in Development to swap in a no-op
    // validator; refuse to run in non-Development if the flag is set.
    wopiHost.WithEnvironment("Wopi__ClientUrl", "http://localhost:9980")
            .WithEnvironment("Wopi__Security__DisableProofValidation", "true")
            .WaitFor(collabora);
}

// Frontends: project references via WithReference give Aspire's service-discovery env vars
// (services__wopihost__http__0=...), but the existing frontend code reads Wopi:HostUrl directly
// from IConfiguration — so we ALSO inject Wopi__HostUrl pointing at the same backend port. That
// way the AppHost owns the URL end-to-end and the frontend's appsettings.json HostUrl entry is
// only the production default.
//
// Collabora mode is special: the frontend's HostUrl is the URL that goes into WopiSrc, which is
// then fetched by Collabora-in-Docker — so it must use host.docker.internal, not localhost. In
// non-Collabora mode there's no in-Docker WOPI client (the real OOS/M365 WOPI clients live
// outside the dev loop and configure their own URL), so localhost is fine for the dev dashboard.
var wopiHostWeb = builder.AddProject<Projects.WopiHost_Web>("wopihost-web", launchProfileName: null)
       .WithReference(wopiHost)
       .WithEnvironment(ctx =>
       {
           var host = useCollabora ? "host.docker.internal" : "localhost";
           ctx.EnvironmentVariables["Wopi__HostUrl"] = $"http://{host}:{wopiBackendEndpoint.Port}";
       })
       .WithHttpsEndpoint()
       .WithExternalHttpEndpoints();

if (useCollabora)
{
    // The frontend embeds Collabora; the iframe URL must come from Collabora's discovery, and
    // the WopiSrc it carries must resolve from inside the Collabora container (host.docker.internal,
    // injected above). Collabora's discovery XML emits a single <net-zone name="external-http">
    // only — defaulting to ExternalHttps (as appsettings does for OOS/M365) silently filters every
    // action and icon out, so files render with the generic icon and edit/view buttons stay disabled.
    wopiHostWeb.WithEnvironment("Wopi__ClientUrl", "http://localhost:9980")
               .WithEnvironment("Wopi__Discovery__NetZone", "ExternalHttp")
               .WaitFor(collabora!);
}

// Validator: same wiring pattern as the Web frontend. The validator never runs against Collabora
// (it's a WOPI protocol checker), so localhost is the right reach.
builder.AddProject<Projects.WopiHost_Validator>("wopihost-validator", launchProfileName: null)
       .WithReference(wopiHost)
       .WithEnvironment(ctx =>
       {
           ctx.EnvironmentVariables["Wopi__HostUrl"] = $"http://localhost:{wopiBackendEndpoint.Port}";
       })
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
    builder.AddProject<Projects.WopiHost_Web_Oidc>("wopihost-web-oidc", launchProfileName: null)
           .WithReference(wopiHost)
           .WithEnvironment(ctx =>
           {
               var host = useCollabora ? "host.docker.internal" : "localhost";
               ctx.EnvironmentVariables["Wopi__HostUrl"] = $"http://{host}:{wopiBackendEndpoint.Port}";
           })
           .WithHttpsEndpoint()
           .WithExternalHttpEndpoints();
}

builder.Build().Run();
