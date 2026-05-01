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

// Add WopiHost.Web frontend that depends on WopiHost
builder.AddProject<Projects.WopiHost_Web>("wopihost-web")
       .WithReference(wopiHost)
       .WithEndpoint(name: "web-http", port: 6000, scheme: "http")
       .WithEndpoint(name: "web-https", port: 6001, scheme: "https")
       .WithExternalHttpEndpoints();

// Add Validator project for testing
builder.AddProject<Projects.WopiHost_Validator>("wopihost-validator")
       .WithReference(wopiHost)
       .WithEndpoint(name: "validator-http", port: 7000, scheme: "http")
       .WithExternalHttpEndpoints();

// Optional: OIDC frontend sample. Opt-in via "AppHost:IncludeOidcSample"=true so newcomers don't
// need to register an IdP just to run the default flow. Requires the user to fill in Oidc:* config
// in sample/WopiHost.Web.Oidc/appsettings.Development.json (see that sample's README for setup).
if (builder.Configuration.GetValue<bool>("AppHost:IncludeOidcSample"))
{
    builder.AddProject<Projects.WopiHost_Web_Oidc>("wopihost-web-oidc")
           .WithReference(wopiHost)
           .WithEndpoint(name: "web-oidc-http", port: 6100, scheme: "http")
           .WithEndpoint(name: "web-oidc-https", port: 6101, scheme: "https")
           .WithExternalHttpEndpoints();
}

builder.Build().Run();
