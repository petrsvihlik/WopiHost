using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Add WopiHost as the backend service
var wopiHost = builder.AddProject<Projects.WopiHost>("wopihost")
                      .WithEndpoint(name: "wopihost-http", port: 5000, scheme: "http")
                      .WithUrlForEndpoint("wopihost-http", url =>
                      {
                          url.DisplayText = "Scalar (HTTP)";
                          url.Url = "/scalar";
                      });

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
// Wiring:
//  - Browser reaches Collabora at http://localhost:9980.
//  - Collabora (in Docker) reaches the WOPI host via host.docker.internal:5000, which Docker
//    Desktop maps to the host. On Linux Docker, run with --add-host=host.docker.internal:host-gateway.
//  - "domain" is a regex (escape dots) of WOPI hosts Collabora is allowed to call back to;
//    a mismatch with the WopiSrc query param yields a silent 401.
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
           .WithEnvironment("domain", "host\\.docker\\.internal:5000")
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

// Add WopiHost.Web frontend that depends on WopiHost. Endpoints come from the project's
// launchSettings.json (HTTPS only — see that file). Duplicating them here with .WithEndpoint
// would cause the dashboard to list the same URL twice.
var wopiHostWeb = builder.AddProject<Projects.WopiHost_Web>("wopihost-web")
       .WithReference(wopiHost)
       .WithExternalHttpEndpoints();

if (useCollabora)
{
    // The frontend embeds Collabora; the iframe URL must come from Collabora's discovery, and
    // the WopiSrc it carries must resolve from inside the Collabora container. Collabora's
    // discovery XML emits a single <net-zone name="external-http"> only — defaulting to
    // ExternalHttps (as appsettings does for OOS/M365) silently filters every action and icon
    // out, so files render with the generic icon and edit/view buttons stay disabled.
    wopiHostWeb.WithEnvironment("Wopi__ClientUrl", "http://localhost:9980")
               .WithEnvironment("Wopi__HostUrl", "http://host.docker.internal:5000")
               .WithEnvironment("Wopi__Discovery__NetZone", "ExternalHttp")
               .WaitFor(collabora!);
}

// Add Validator project for testing. Endpoints come from launchSettings.json (HTTPS only).
builder.AddProject<Projects.WopiHost_Validator>("wopihost-validator")
       .WithReference(wopiHost)
       .WithExternalHttpEndpoints();

// Optional: OIDC frontend sample. Opt-in via "AppHost:IncludeOidcSample"=true so newcomers don't
// need to register an IdP just to run the default flow. Requires the user to fill in Oidc:* config
// in sample/WopiHost.Web.Oidc/appsettings.Development.json (see that sample's README for setup).
if (builder.Configuration.GetValue<bool>("AppHost:IncludeOidcSample"))
{
    // Endpoints come from the project's launchSettings.json (HTTPS only). OIDC requires HTTPS
    // for cookie/redirect-URI sanity anyway.
    builder.AddProject<Projects.WopiHost_Web_Oidc>("wopihost-web-oidc")
           .WithReference(wopiHost)
           .WithExternalHttpEndpoints();
}

builder.Build().Run();
