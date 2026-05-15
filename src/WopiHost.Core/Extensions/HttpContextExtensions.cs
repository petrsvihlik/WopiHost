using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Helpers for Minimal-API endpoint handlers that need to reuse infrastructure currently
/// shaped for MVC controllers (notably <see cref="IUrlHelper"/>-based URL generation).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Phase 2 of #430 migration; exercised by HTTP parity tests in phase 5 (test relocation into WopiHost.IntegrationTests)")]
internal static class HttpContextExtensions
{
    /// <summary>
    /// Constructs an <see cref="IUrlHelper"/> bound to the current request so endpoint handlers
    /// can call the existing <c>GetWopiSrc(...)</c> extensions unchanged. The wrapper assembles
    /// the minimum <see cref="ActionContext"/> needed by <see cref="IUrlHelperFactory"/> —
    /// <see cref="RouteData"/> from the request plus an empty <see cref="ActionDescriptor"/>.
    /// </summary>
    public static IUrlHelper GetUrlHelper(this HttpContext httpContext)
    {
        var routeData = httpContext.GetRouteData();
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        return httpContext.RequestServices.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(actionContext);
    }
}
