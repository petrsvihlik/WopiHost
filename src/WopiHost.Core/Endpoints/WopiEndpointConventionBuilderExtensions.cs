using Microsoft.AspNetCore.Builder;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Provides endpoint-convention-builder extensions for WOPI-specific authorization.
/// </summary>
public static class WopiEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Attaches a per-resource WOPI authorization requirement
    /// (<see cref="WopiAuthorizeAttribute"/>) to the endpoint. Equivalent to
    /// <c>RequireAuthorization(p =&gt; p.AddRequirements(new WopiAuthorizeAttribute(resource, permission)))</c>.
    /// </summary>
    /// <typeparam name="TBuilder">The convention-builder type (e.g. <see cref="RouteHandlerBuilder"/>).</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="resource">The WOPI resource type the authorization gate applies to.</param>
    /// <param name="permission">The WOPI permission the caller must hold.</param>
    /// <returns>The same <paramref name="builder"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TBuilder RequireWopiPermission<TBuilder>(
        this TBuilder builder,
        WopiResourceType resource,
        Permission permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(resource, permission)));
    }
}
