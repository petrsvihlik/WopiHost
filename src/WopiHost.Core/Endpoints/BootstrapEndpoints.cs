using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Minimal-API endpoints for the <c>/wopibootstrapper</c> surface. Lives outside the
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
internal static class BootstrapEndpoints
{
    public static void MapBootstrapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // MapGroup("/wopibootstrapper") + MapGet("") normalises the template to
        // "/wopibootstrapper/" (trailing slash) — see MapGroupEmptyTemplateTests — so register
        // the two verbs directly on the receiver instead.
        static void ApplyBootstrapAuth(Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder p) => p
            .AddAuthenticationSchemes(WopiAuthenticationSchemes.Bootstrap)
            .RequireAuthenticatedUser();

        endpoints.MapGet("/wopibootstrapper", Bootstrap)
            .WithTags("Bootstrap")
            .WithSummary("WOPI Bootstrap — issues an ecosystem access token for the authenticated principal.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap. " +
                "Authenticates with the WopiAuthenticationSchemes.Bootstrap scheme (OAuth2 Bearer from the IdP).")
            .RequireAuthorization(ApplyBootstrapAuth)
            .AddEndpointFilter<WopiOriginValidationEndpointFilter>()
            .AddEndpointFilter<WopiTelemetryEndpointFilter>();

        endpoints.MapPost("/wopibootstrapper", ExecuteEcosystemOperation)
            .WithTags("Bootstrap")
            .WithSummary("Dispatches by X-WOPI-EcosystemOperation header (GET_ROOT_CONTAINER | GET_NEW_ACCESS_TOKEN).")
            .WithDescription("GET_ROOT_CONTAINER: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer. " +
                "GET_NEW_ACCESS_TOKEN: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getnewaccesstoken. " +
                "Unknown operation values yield 501.")
            .RequireAuthorization(ApplyBootstrapAuth)
            .AddEndpointFilter<WopiOriginValidationEndpointFilter>()
            .AddEndpointFilter<WopiTelemetryEndpointFilter>();
    }

    private static async Task<JsonHttpResult<BootstrapRootContainerInfo>> Bootstrap(
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

    private static async Task<Results<NotFound, JsonHttpResult<BootstrapRootContainerInfo>, StatusCodeHttpResult>> ExecuteEcosystemOperation(
        [AsParameters] ExecuteEcosystemOperationRequest req) => req.EcosystemOperation switch
    {
        "GET_ROOT_CONTAINER" => await GetRootContainerAsync(req).ConfigureAwait(false),
        "GET_NEW_ACCESS_TOKEN" => await GetNewAccessTokenAsync(req).ConfigureAwait(false),
        _ => TypedResults.StatusCode(StatusCodes.Status501NotImplemented),
    };

    private static async Task<Results<NotFound, JsonHttpResult<BootstrapRootContainerInfo>, StatusCodeHttpResult>> GetRootContainerAsync(
        ExecuteEcosystemOperationRequest req)
    {
        var userId = GetUserIdOrThrow(req.Http.User);
        var ecosystemToken = await req.AccessTokenService.IssueAsync(BuildEcosystemTokenRequest(req.Http.User, userId, req.Storage), req.CancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(req.Http, userId, ecosystemToken);

        var rootContainer = await req.Storage.GetWopiContainer(req.Storage.RootContainer.Identifier, req.CancellationToken).ConfigureAwait(false);
        if (rootContainer is null) return TypedResults.NotFound();

        var token = await IssueContainerTokenAsync(req.Http, req.AccessTokenService, req.PermissionProvider, rootContainer, req.CancellationToken).ConfigureAwait(false);
        return TypedResults.Json(new BootstrapRootContainerInfo
        {
            Bootstrap = bootstrap,
            RootContainerInfo = new RootContainerInfo
            {
                ContainerPointer = new ChildContainer(rootContainer.Name, req.Http.GetWopiSrc(rootContainer, token.Token)),
                ContainerInfo = await req.ContainerInfoBuilder.BuildAsync(rootContainer, req.Http.User, req.CancellationToken).ConfigureAwait(false),
            },
        });
    }

    private static async Task<Results<NotFound, JsonHttpResult<BootstrapRootContainerInfo>, StatusCodeHttpResult>> GetNewAccessTokenAsync(
        ExecuteEcosystemOperationRequest req)
    {
        // Spec: if X-WOPI-WopiSrc is absent or unparseable, return 404.
        if (string.IsNullOrEmpty(req.WopiSrc) || !EndpointHelpers.TryParseWopiSrc(req.WopiSrc, out var resourceType, out var resourceId))
        {
            return TypedResults.NotFound();
        }

        var userId = GetUserIdOrThrow(req.Http.User);
        var ecosystemToken = await req.AccessTokenService.IssueAsync(BuildEcosystemTokenRequest(req.Http.User, userId, req.Storage), req.CancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(req.Http, userId, ecosystemToken);
        WopiAccessToken token;

        if (resourceType == WopiResourceType.File)
        {
            var file = await req.Storage.GetWopiFile(resourceId, req.CancellationToken).ConfigureAwait(false);
            // Spec: only provide a token if the requested WopiSrc exists and the user is authorized.
            if (file is null) return TypedResults.NotFound();
            token = await IssueFileTokenAsync(req.Http, req.AccessTokenService, req.PermissionProvider, file, req.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            var container = await req.Storage.GetWopiContainer(resourceId, req.CancellationToken).ConfigureAwait(false);
            if (container is null) return TypedResults.NotFound();
            token = await IssueContainerTokenAsync(req.Http, req.AccessTokenService, req.PermissionProvider, container, req.CancellationToken).ConfigureAwait(false);
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

/// <summary>Parameter bundle for <see cref="BootstrapEndpoints.ExecuteEcosystemOperation"/>.</summary>
internal readonly record struct ExecuteEcosystemOperationRequest(
    HttpContext Http,
    IWopiStorageProvider Storage,
    IWopiAccessTokenService AccessTokenService,
    IWopiPermissionProvider PermissionProvider,
    ICheckContainerInfoBuilder ContainerInfoBuilder,
    [FromHeader(Name = WopiHeaders.ECOSYSTEM_OPERATION)] string? EcosystemOperation,
    [FromHeader(Name = WopiHeaders.WOPI_SRC)] string? WopiSrc,
    CancellationToken CancellationToken);
