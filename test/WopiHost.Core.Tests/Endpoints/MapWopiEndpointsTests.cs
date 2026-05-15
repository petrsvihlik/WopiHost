using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Endpoints;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security;

namespace WopiHost.Core.Tests.Endpoints;

/// <summary>
/// Verifies that <see cref="WopiEndpointRouteBuilderExtensions.MapWopiEndpoints"/> registers
/// the expected route table — names, templates, and resource-kind metadata. Full HTTP parity
/// tests against the controller behaviour land in Phase 5 of the #430 migration.
/// </summary>
public sealed class MapWopiEndpointsTests : IAsyncLifetime
{
    private WebApplication? _app;
    private RouteEndpoint[] _endpoints = [];

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        // AddWopi registers every service the endpoint handlers consume, so Minimal-API
        // registration-time DI discovery doesn't fall back to body inference for unrecognised
        // service parameter types (which would error out on GET endpoints).
        builder.Services.AddWopi();
        builder.Services.AddOptions<WopiSecurityOptions>().Configure(o => o.SigningKey = new byte[32]);
        // Storage provider isn't part of AddWopi (it's provider-package-scoped) — stub it so
        // registration discovery sees the type as a service.
        builder.Services.AddSingleton(Mock.Of<IWopiStorageProvider>());
        // Override the default WOPI auth scheme so RequireAuthorization() resolves; the no-op
        // handler short-circuits to NoResult since we never hit endpoints in this test.
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, NoOpAuthHandler>("test", _ => { });

        _app = builder.Build();
        _app.MapWopiEndpoints();
        await _app.StartAsync();

        _endpoints = _app.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
    }

    [Theory]
    [InlineData(WopiRouteNames.CheckFileInfo, "/wopi/files/{id}")]
    [InlineData(WopiRouteNames.GetFile, "/wopi/files/{id}/contents")]
    [InlineData(WopiRouteNames.CheckContainerInfo, "/wopi/containers/{id}")]
    [InlineData(WopiRouteNames.CheckFolderInfo, "/wopi/folders/{id}")]
    [InlineData(WopiRouteNames.CheckEcosystem, "/wopi/ecosystem")]
    public void Named_Routes_Have_Expected_Templates(string routeName, string expectedTemplate)
    {
        var endpoint = _endpoints.FirstOrDefault(e => e.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName == routeName);
        Assert.NotNull(endpoint);
        Assert.Equal(expectedTemplate, endpoint.RoutePattern.RawText);
    }

    [Fact]
    public void Containers_Group_Carries_Container_ResourceKindMetadata()
    {
        var endpoint = _endpoints.First(e => e.RoutePattern.RawText == "/wopi/containers/{id}");
        var kind = endpoint.Metadata.GetMetadata<WopiResourceKindMetadata>();
        Assert.NotNull(kind);
        Assert.Equal(WopiResourceType.Container, kind.Type);
    }

    [Fact]
    public void Folders_Group_Carries_Container_ResourceKindMetadata()
    {
        var endpoint = _endpoints.First(e => e.RoutePattern.RawText == "/wopi/folders/{id}");
        var kind = endpoint.Metadata.GetMetadata<WopiResourceKindMetadata>();
        Assert.NotNull(kind);
        Assert.Equal(WopiResourceType.Container, kind.Type);
    }

    [Fact]
    public void Files_Group_Does_Not_Carry_ResourceKindMetadata()
    {
        // File is the default — endpoints under /wopi/files leave the metadata off so the
        // telemetry filter's fallback to FileId tag applies.
        var endpoint = _endpoints.First(e => e.RoutePattern.RawText == "/wopi/files/{id}");
        Assert.Null(endpoint.Metadata.GetMetadata<WopiResourceKindMetadata>());
    }

    [Fact]
    public void Endpoint_Table_Contains_Expected_GET_Routes()
    {
        var templates = _endpoints
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true)
            .Select(e => e.RoutePattern.RawText)
            .ToHashSet();

        // 12 read-only endpoints land in Phase 2. Bootstrap GET defers to Phase 3.
        string[] expected =
        [
            "/wopi/files/{id}",
            "/wopi/files/{id}/contents",
            "/wopi/files/{id}/ecosystem_pointer",
            "/wopi/files/{id}/ancestry",
            "/wopi/containers/{id}",
            "/wopi/containers/{id}/ecosystem_pointer",
            "/wopi/containers/{id}/ancestry",
            "/wopi/containers/{id}/children",
            "/wopi/folders/{id}",
            "/wopi/folders/{id}/children",
            "/wopi/ecosystem",
            "/wopi/ecosystem/root_container_pointer",
        ];
        foreach (var template in expected)
        {
            Assert.Contains(template, templates);
        }
    }

    private sealed class NoOpAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());
    }
}
