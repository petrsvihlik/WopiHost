using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Minimal-API endpoint registration for the WOPI host. The Minimal-API equivalent of the
/// historic <c>app.MapControllers()</c> path when consuming <c>WopiHost.Core</c>.
/// </summary>
/// <remarks>
/// <para>
/// Consumer usage:
/// <code>
/// builder.Services.AddWopi();
/// // ...
/// app.MapWopiEndpoints();
/// </code>
/// </para>
/// <para>
/// During the migration tracked in issue #430 the controller topology is still wired by the
/// sample. <c>MapWopiEndpoints()</c> is the forward-looking entry point and is safe to call
/// alongside <c>app.MapControllers()</c> during transition <em>provided</em> the route
/// templates do not collide — for the read-only GET endpoints they would, so a consumer
/// experimenting with the new topology should pick one or the other.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Phase 2 of #430 migration; HTTP parity tests land in phase 5 (test relocation into WopiHost.IntegrationTests). Route-table assertions live in MapWopiEndpointsTests.")]
public static class WopiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the WOPI host endpoints onto <paramref name="endpoints"/>. Returns a route group
    /// builder so additional metadata can be layered on by the caller.
    /// </summary>
    public static IEndpointRouteBuilder MapWopiEndpoints(this IEndpointRouteBuilder endpoints)
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

        return endpoints;
    }
}
