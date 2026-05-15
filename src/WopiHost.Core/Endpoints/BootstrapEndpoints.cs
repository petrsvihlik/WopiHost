using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Minimal-API equivalents of <c>WopiBootstrapperController</c>. Lives outside the
/// <c>/wopi</c> group because the bootstrap endpoints authenticate via
/// <see cref="WopiAuthenticationSchemes.Bootstrap"/> (OAuth2 Bearer from the host's IdP)
/// rather than the access-token query parameter the rest of the WOPI surface uses.
/// </summary>
/// <remarks>
/// Three operations share <c>/wopibootstrapper</c>:
/// <list type="bullet">
///   <item><description><c>GET</c> — bare <c>Bootstrap</c> per
///   <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap"/></description></item>
///   <item><description><c>POST</c> with <c>X-WOPI-EcosystemOperation: GET_ROOT_CONTAINER</c> per
///   <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer"/></description></item>
///   <item><description><c>POST</c> with <c>X-WOPI-EcosystemOperation: GET_NEW_ACCESS_TOKEN</c> per
///   <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getnewaccesstoken"/></description></item>
/// </list>
/// The POST dispatch reads its own header (<see cref="WopiHeaders.ECOSYSTEM_OPERATION"/>)
/// rather than <c>X-WOPI-Override</c>, so it skips <see cref="WopiOverrideMatcherPolicy"/>
/// and switches inside the handler — two operations doesn't justify a parallel matcher
/// policy.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Phase 5 of #430: bootstrap requires a separate test fixture for the OAuth2-Bearer auth scheme. Coverage is tracked as a follow-up issue. The Microsoft WOPI validator job in CI exercises bootstrap end-to-end as protocol-conformance.")]
internal static class BootstrapEndpoints
{
    public static void MapBootstrapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Two endpoints; MapGroup("/wopibootstrapper") + MapGet("") would normalise the
        // template wrong (same trap that bit /ecosystem in EcosystemEndpoints), so register
        // the two verbs directly.
        static void ApplyBootstrapAuth(Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder p) => p
            .AddAuthenticationSchemes(WopiAuthenticationSchemes.Bootstrap)
            .RequireAuthenticatedUser();

        endpoints.MapGet("/wopibootstrapper", Bootstrap)
            .RequireAuthorization(ApplyBootstrapAuth)
            .AddEndpointFilter<Security.Authentication.WopiOriginValidationEndpointFilter>()
            .AddEndpointFilter<WopiTelemetryEndpointFilter>();

        endpoints.MapPost("/wopibootstrapper", ExecuteEcosystemOperation)
            .RequireAuthorization(ApplyBootstrapAuth)
            .AddEndpointFilter<Security.Authentication.WopiOriginValidationEndpointFilter>()
            .AddEndpointFilter<WopiTelemetryEndpointFilter>();
    }

    /// <summary>Bundle of services consumed by both bootstrap handlers.</summary>
    internal sealed record BootstrapDeps(
        IWopiStorageProvider Storage,
        IWopiAccessTokenService AccessTokenService,
        IWopiPermissionProvider PermissionProvider,
        ICheckContainerInfoBuilder ContainerInfoBuilder);

    private static async Task<IResult> Bootstrap(
        HttpContext httpContext,
        [AsParameters] BootstrapDeps deps,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdOrThrow(httpContext.User);
        var ecosystemToken = await deps.AccessTokenService.IssueAsync(BuildEcosystemTokenRequest(httpContext.User, userId, deps.Storage), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(httpContext, userId, ecosystemToken);
        return TypedResults.Json(new BootstrapRootContainerInfo { Bootstrap = bootstrap });
    }

    private static async Task<IResult> ExecuteEcosystemOperation(
        HttpContext httpContext,
        [AsParameters] BootstrapDeps deps,
        [FromHeader(Name = WopiHeaders.ECOSYSTEM_OPERATION)] string? ecosystemOperation,
        [FromHeader(Name = WopiHeaders.WOPI_SRC)] string? wopiSrc,
        CancellationToken cancellationToken) => ecosystemOperation switch
    {
        "GET_ROOT_CONTAINER" => await GetRootContainerAsync(httpContext, deps, cancellationToken).ConfigureAwait(false),
        "GET_NEW_ACCESS_TOKEN" => await GetNewAccessTokenAsync(httpContext, deps, wopiSrc, cancellationToken).ConfigureAwait(false),
        _ => TypedResults.StatusCode(StatusCodes.Status501NotImplemented),
    };

    private static async Task<IResult> GetRootContainerAsync(HttpContext httpContext, BootstrapDeps deps, CancellationToken cancellationToken)
    {
        var userId = GetUserIdOrThrow(httpContext.User);
        var ecosystemToken = await deps.AccessTokenService.IssueAsync(BuildEcosystemTokenRequest(httpContext.User, userId, deps.Storage), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(httpContext, userId, ecosystemToken);

        var rootContainer = await deps.Storage.GetWopiContainer(deps.Storage.RootContainer.Identifier, cancellationToken).ConfigureAwait(false);
        if (rootContainer is null) return TypedResults.NotFound();

        var token = await IssueContainerTokenAsync(httpContext, deps, rootContainer, cancellationToken).ConfigureAwait(false);
        var url = httpContext.GetUrlHelper();
        return TypedResults.Json(new BootstrapRootContainerInfo
        {
            Bootstrap = bootstrap,
            RootContainerInfo = new RootContainerInfo
            {
                ContainerPointer = new ChildContainer(rootContainer.Name, url.GetWopiSrc(rootContainer, token.Token)),
                ContainerInfo = await deps.ContainerInfoBuilder.BuildAsync(rootContainer, httpContext, cancellationToken).ConfigureAwait(false),
            },
        });
    }

    private static async Task<IResult> GetNewAccessTokenAsync(HttpContext httpContext, BootstrapDeps deps, string? wopiSrc, CancellationToken cancellationToken)
    {
        // Spec: if X-WOPI-WopiSrc is absent or unparseable, return 404.
        if (string.IsNullOrEmpty(wopiSrc) || !EndpointHelpers.TryParseWopiSrc(wopiSrc, out var resourceType, out var resourceId))
        {
            return TypedResults.NotFound();
        }

        var userId = GetUserIdOrThrow(httpContext.User);
        var ecosystemToken = await deps.AccessTokenService.IssueAsync(BuildEcosystemTokenRequest(httpContext.User, userId, deps.Storage), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(httpContext, userId, ecosystemToken);
        WopiAccessToken token;

        if (resourceType == WopiResourceType.File)
        {
            var file = await deps.Storage.GetWopiFile(resourceId, cancellationToken).ConfigureAwait(false);
            // Spec: only provide a token if the requested WopiSrc exists and the user is authorized.
            if (file is null) return TypedResults.NotFound();
            token = await IssueFileTokenAsync(httpContext, deps, file, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var container = await deps.Storage.GetWopiContainer(resourceId, cancellationToken).ConfigureAwait(false);
            if (container is null) return TypedResults.NotFound();
            token = await IssueContainerTokenAsync(httpContext, deps, container, cancellationToken).ConfigureAwait(false);
        }

        return TypedResults.Json(new BootstrapRootContainerInfo
        {
            Bootstrap = bootstrap,
            AccessTokenInfo = new AccessTokenInfo
            {
                AccessToken = token.Token,
                AccessTokenExpiry = token.ExpiresAt.ToUnixTimeSeconds(),
            },
        });
    }

    private static WopiAccessTokenRequest BuildEcosystemTokenRequest(ClaimsPrincipal user, string userId, IWopiStorageProvider storage) => new()
    {
        UserId = userId,
        UserDisplayName = user.FindFirstValue(ClaimTypes.Name),
        UserEmail = user.FindFirstValue(ClaimTypes.Email),
        ResourceId = storage.RootContainer.Identifier,
        ResourceType = WopiResourceType.Container,
        ContainerPermissions = WopiContainerPermissions.None,
    };

    private static BootstrapInfo BuildBootstrapInfo(HttpContext httpContext, string userId, WopiAccessToken ecosystemToken)
    {
        var url = httpContext.GetUrlHelper();
        return new BootstrapInfo
        {
            EcosystemUrl = url.GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: ecosystemToken.Token),
            UserId = userId,
            SignInName = httpContext.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            UserFriendlyName = httpContext.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
        };
    }

    private static async Task<WopiAccessToken> IssueFileTokenAsync(HttpContext httpContext, BootstrapDeps deps, IWopiFile file, CancellationToken cancellationToken)
    {
        var perms = await deps.PermissionProvider.GetFilePermissionsAsync(httpContext.User, file, cancellationToken).ConfigureAwait(false);
        return await deps.AccessTokenService.IssueAsync(BuildRequest(httpContext.User, file.Identifier, WopiResourceType.File) with
        {
            FilePermissions = perms,
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WopiAccessToken> IssueContainerTokenAsync(HttpContext httpContext, BootstrapDeps deps, IWopiContainer container, CancellationToken cancellationToken)
    {
        var perms = await deps.PermissionProvider.GetContainerPermissionsAsync(httpContext.User, container, cancellationToken).ConfigureAwait(false);
        return await deps.AccessTokenService.IssueAsync(BuildRequest(httpContext.User, container.Identifier, WopiResourceType.Container) with
        {
            ContainerPermissions = perms,
        }, cancellationToken).ConfigureAwait(false);
    }

    private static WopiAccessTokenRequest BuildRequest(ClaimsPrincipal user, string resourceId, WopiResourceType resourceType) => new()
    {
        UserId = GetUserIdOrThrow(user),
        UserDisplayName = user.FindFirstValue(ClaimTypes.Name),
        UserEmail = user.FindFirstValue(ClaimTypes.Email),
        ResourceId = resourceId,
        ResourceType = resourceType,
    };

    private static string GetUserIdOrThrow(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue(ClaimTypes.Upn)
        ?? throw new InvalidOperationException("Bootstrap principal lacks an identifier claim.");
}
