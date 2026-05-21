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
/// Minimal-API bootstrapper endpoints. Live outside the <c>/wopi</c> group because the
/// bootstrap endpoints authenticate via <see cref="WopiAuthenticationSchemes.Bootstrap"/>
/// (OAuth2 Bearer from the host's IdP) rather than the access-token query parameter the rest
/// of the WOPI surface uses.
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

    private static async Task<IResult> Bootstrap(
        HttpContext httpContext,
        IWopiStorageProvider storage,
        IWopiAccessTokenService accessTokenService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdOrThrow(httpContext.User);
        var ecosystemToken = await accessTokenService.IssueAsync(BuildEcosystemTokenRequest(httpContext.User, userId, storage), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(httpContext, userId, ecosystemToken);
        return TypedResults.Json(new BootstrapRootContainerInfo { Bootstrap = bootstrap });
    }

    private static async Task<IResult> ExecuteEcosystemOperation(
        HttpContext httpContext,
        IWopiStorageProvider storage,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        ICheckContainerInfoBuilder containerInfoBuilder,
        [FromHeader(Name = WopiHeaders.ECOSYSTEM_OPERATION)] string? ecosystemOperation,
        [FromHeader(Name = WopiHeaders.WOPI_SRC)] string? wopiSrc,
        CancellationToken cancellationToken) => ecosystemOperation switch
    {
        "GET_ROOT_CONTAINER" => await GetRootContainerAsync(httpContext, storage, accessTokenService, permissionProvider, containerInfoBuilder, cancellationToken).ConfigureAwait(false),
        "GET_NEW_ACCESS_TOKEN" => await GetNewAccessTokenAsync(httpContext, storage, accessTokenService, permissionProvider, wopiSrc, cancellationToken).ConfigureAwait(false),
        _ => TypedResults.StatusCode(StatusCodes.Status501NotImplemented),
    };

    private static async Task<IResult> GetRootContainerAsync(
        HttpContext httpContext,
        IWopiStorageProvider storage,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        ICheckContainerInfoBuilder containerInfoBuilder,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdOrThrow(httpContext.User);
        var ecosystemToken = await accessTokenService.IssueAsync(BuildEcosystemTokenRequest(httpContext.User, userId, storage), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(httpContext, userId, ecosystemToken);

        var rootContainer = await storage.GetWopiContainer(storage.RootContainer.Identifier, cancellationToken).ConfigureAwait(false);
        if (rootContainer is null) return TypedResults.NotFound();

        var token = await IssueContainerTokenAsync(httpContext, accessTokenService, permissionProvider, rootContainer, cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(new BootstrapRootContainerInfo
        {
            Bootstrap = bootstrap,
            RootContainerInfo = new RootContainerInfo
            {
                ContainerPointer = new ChildContainer(rootContainer.Name, httpContext.GetWopiSrc(rootContainer, token.Token)),
                ContainerInfo = await containerInfoBuilder.BuildAsync(rootContainer, httpContext, cancellationToken).ConfigureAwait(false),
            },
        });
    }

    private static async Task<IResult> GetNewAccessTokenAsync(
        HttpContext httpContext,
        IWopiStorageProvider storage,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        string? wopiSrc,
        CancellationToken cancellationToken)
    {
        // Spec: if X-WOPI-WopiSrc is absent or unparseable, return 404.
        if (string.IsNullOrEmpty(wopiSrc) || !EndpointHelpers.TryParseWopiSrc(wopiSrc, out var resourceType, out var resourceId))
        {
            return TypedResults.NotFound();
        }

        var userId = GetUserIdOrThrow(httpContext.User);
        var ecosystemToken = await accessTokenService.IssueAsync(BuildEcosystemTokenRequest(httpContext.User, userId, storage), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(httpContext, userId, ecosystemToken);
        WopiAccessToken token;

        if (resourceType == WopiResourceType.File)
        {
            var file = await storage.GetWopiFile(resourceId, cancellationToken).ConfigureAwait(false);
            // Spec: only provide a token if the requested WopiSrc exists and the user is authorized.
            if (file is null) return TypedResults.NotFound();
            token = await IssueFileTokenAsync(httpContext, accessTokenService, permissionProvider, file, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var container = await storage.GetWopiContainer(resourceId, cancellationToken).ConfigureAwait(false);
            if (container is null) return TypedResults.NotFound();
            token = await IssueContainerTokenAsync(httpContext, accessTokenService, permissionProvider, container, cancellationToken).ConfigureAwait(false);
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

    private static BootstrapInfo BuildBootstrapInfo(HttpContext httpContext, string userId, WopiAccessToken ecosystemToken) => new()
    {
        EcosystemUrl = httpContext.GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: ecosystemToken.Token),
        UserId = userId,
        SignInName = httpContext.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
        UserFriendlyName = httpContext.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
    };

    private static async Task<WopiAccessToken> IssueFileTokenAsync(HttpContext httpContext, IWopiAccessTokenService accessTokenService, IWopiPermissionProvider permissionProvider, IWopiFile file, CancellationToken cancellationToken)
    {
        var perms = await permissionProvider.GetFilePermissionsAsync(httpContext.User, file, cancellationToken).ConfigureAwait(false);
        return await accessTokenService.IssueAsync(BuildRequest(httpContext.User, file.Identifier, WopiResourceType.File) with
        {
            FilePermissions = perms,
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WopiAccessToken> IssueContainerTokenAsync(HttpContext httpContext, IWopiAccessTokenService accessTokenService, IWopiPermissionProvider permissionProvider, IWopiContainer container, CancellationToken cancellationToken)
    {
        var perms = await permissionProvider.GetContainerPermissionsAsync(httpContext.User, container, cancellationToken).ConfigureAwait(false);
        return await accessTokenService.IssueAsync(BuildRequest(httpContext.User, container.Identifier, WopiResourceType.Container) with
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
