using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Minimal-API endpoint registration for the WOPI host. Call alongside <c>AddWopi()</c>:
/// <code>
/// builder.Services.AddWopi();
/// // ...
/// app.MapWopiEndpoints();
/// </code>
/// </summary>
public static class WopiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the WOPI host endpoints onto <paramref name="endpoints"/>. Returns the
    /// <c>/wopi</c> route group so additional metadata can be layered on by the caller
    /// (the bootstrap endpoints are registered as a side effect and aren't reachable
    /// through the returned builder — they live on a different prefix and auth scheme).
    /// </summary>
    public static RouteGroupBuilder MapWopiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // /wopi route group: WOPI auth scheme, proof-key validation, telemetry. Resource-level
        // authorization (WopiAuthorize) attaches per endpoint. WopiOriginValidation must run
        // after auth has materialised the principal — group RequireAuthorization() guarantees
        // that ordering because authorization middleware runs before endpoint filters.
        var wopi = endpoints.MapGroup("/wopi")
            .RequireAuthorization()
            .AddEndpointFilter<WopiOriginValidationEndpointFilter>()
            .AddEndpointFilter<WopiTelemetryEndpointFilter>();

        FileEndpoints.MapFileEndpoints(wopi);
        ContainerEndpoints.MapContainerEndpoints(wopi);
        FolderEndpoints.MapFolderEndpoints(wopi);
        EcosystemEndpoints.MapEcosystemEndpoints(wopi);

        // /wopibootstrapper sits outside the /wopi group — different auth scheme
        // (WopiAuthenticationSchemes.Bootstrap, OAuth2 Bearer) and the proof-validation /
        // telemetry filters wire up separately inside MapBootstrapEndpoints.
        BootstrapEndpoints.MapBootstrapEndpoints(endpoints);

        return wopi;
    }
}
