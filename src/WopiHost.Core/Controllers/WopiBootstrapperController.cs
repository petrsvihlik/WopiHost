using System.Net.Mime;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Controllers;

/// <summary>
/// WOPI bootstrapper endpoint per
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap"/>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the regular <c>/wopi/*</c> endpoints (which authenticate via the
/// <c>access_token</c> query parameter), the bootstrapper is called by Office mobile clients
/// with an OAuth2 Bearer token from the host's identity provider. Configure the corresponding
/// scheme as <see cref="WopiAuthenticationSchemes.Bootstrap"/>, and use
/// <see cref="WopiBootstrapChallenge"/> to emit the spec-compliant <c>WWW-Authenticate</c>
/// challenge from the scheme's challenge handler.
/// </para>
/// <para>
/// Three operations share this single endpoint, dispatched by HTTP method and the
/// <c>X-WOPI-EcosystemOperation</c> header:
/// </para>
/// <list type="bullet">
///   <item><description><c>GET /wopibootstrapper</c> — bare <c>Bootstrap</c> per <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap"/></description></item>
///   <item><description><c>POST /wopibootstrapper</c> with <c>X-WOPI-EcosystemOperation: GET_ROOT_CONTAINER</c> per <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer"/></description></item>
///   <item><description><c>POST /wopibootstrapper</c> with <c>X-WOPI-EcosystemOperation: GET_NEW_ACCESS_TOKEN</c> per <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getnewaccesstoken"/></description></item>
/// </list>
/// </remarks>
[Authorize(AuthenticationSchemes = WopiAuthenticationSchemes.Bootstrap)]
[ApiController]
[Route("wopibootstrapper")]
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
[ServiceFilter(typeof(WopiTelemetryActionFilter))]
public class WopiBootstrapperController(
    IWopiStorageProvider storageProvider,
    IWopiAccessTokenService accessTokenService,
    IWopiPermissionProvider permissionProvider,
    ICheckContainerInfoBuilder checkContainerInfoBuilder) : ControllerBase
{
    /// <summary>
    /// Bootstrap operation. Returns the bare <see cref="BootstrapInfo"/> needed for the WOPI
    /// client to discover the host's ecosystem endpoint and the current user.
    /// </summary>
    [HttpGet]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> Bootstrap(CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdOrThrow();
        // Direct interface await keeps Infer# happy — see #363; the previous private async
        // helper (BuildBootstrapInfoAsync) became an Infer null-deref FP that no analyzer
        // annotation could suppress. Sync helpers do the request-shape and result-shape work.
        var ecosystemToken = await accessTokenService.IssueAsync(BuildEcosystemTokenRequest(userId), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(userId, ecosystemToken);
        return new JsonResult(new BootstrapRootContainerInfo { Bootstrap = bootstrap });
    }

    /// <summary>
    /// Dispatches POST-based ecosystem operations exposed on the bootstrapper.
    /// </summary>
    [HttpPost]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> ExecuteEcosystemOperation(
        [FromHeader(Name = WopiHeaders.ECOSYSTEM_OPERATION)] string? ecosystemOperation = null,
        [FromHeader(Name = WopiHeaders.WOPI_SRC)] string? wopiSrc = null,
        CancellationToken cancellationToken = default)
    {
        return ecosystemOperation switch
        {
            "GET_ROOT_CONTAINER" => await GetRootContainerAsync(cancellationToken).ConfigureAwait(false),
            "GET_NEW_ACCESS_TOKEN" => await GetNewAccessTokenAsync(wopiSrc, cancellationToken).ConfigureAwait(false),
            _ => new NotImplementedResult(),
        };
    }

    private async Task<IActionResult> GetRootContainerAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserIdOrThrow();
        var ecosystemToken = await accessTokenService.IssueAsync(BuildEcosystemTokenRequest(userId), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(userId, ecosystemToken);

        var rootContainer = await storageProvider.GetWopiContainer(storageProvider.RootContainer.Identifier, cancellationToken).ConfigureAwait(false);
        if (rootContainer is null)
        {
            return NotFound();
        }

        var token = await IssueContainerTokenAsync(rootContainer, cancellationToken).ConfigureAwait(false);
        return new JsonResult(new BootstrapRootContainerInfo
        {
            Bootstrap = bootstrap,
            RootContainerInfo = new RootContainerInfo
            {
                ContainerPointer = new ChildContainer(
                    rootContainer.Name,
                    Url.GetWopiSrc(WopiResourceType.Container, rootContainer.Identifier, token.Token)),
                ContainerInfo = await checkContainerInfoBuilder.BuildAsync(rootContainer, HttpContext, cancellationToken).ConfigureAwait(false),
            },
        });
    }

    private async Task<IActionResult> GetNewAccessTokenAsync(string? wopiSrc, CancellationToken cancellationToken)
    {
        // Spec (https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getnewaccesstoken#request-headers):
        //   "if the X-WOPI-WopiSrc header is not present, the host should return a 404 Not Found"
        if (string.IsNullOrEmpty(wopiSrc) || !TryParseWopiSrc(wopiSrc, out var resourceType, out var resourceId))
        {
            return NotFound();
        }

        var userId = GetUserIdOrThrow();
        var ecosystemToken = await accessTokenService.IssueAsync(BuildEcosystemTokenRequest(userId), cancellationToken).ConfigureAwait(false);
        var bootstrap = BuildBootstrapInfo(userId, ecosystemToken);
        WopiAccessToken token;

        if (resourceType == WopiResourceType.File)
        {
            var file = await storageProvider.GetWopiFile(resourceId, cancellationToken).ConfigureAwait(false);
            // Spec: "must only provide a WOPI access token if the requested WopiSrc exists
            // and the user is authorized to access it."
            if (file is null)
            {
                return NotFound();
            }
            token = await IssueFileTokenAsync(file, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var container = await storageProvider.GetWopiContainer(resourceId, cancellationToken).ConfigureAwait(false);
            if (container is null)
            {
                return NotFound();
            }
            token = await IssueContainerTokenAsync(container, cancellationToken).ConfigureAwait(false);
        }

        return new JsonResult(new BootstrapRootContainerInfo
        {
            Bootstrap = bootstrap,
            AccessTokenInfo = new AccessTokenInfo
            {
                AccessToken = token.Token,
                AccessTokenExpiry = token.ExpiresAt.ToUnixTimeSeconds(),
            },
        });
    }

    /// <summary>
    /// Builds the access-token request for the bootstrap ecosystem token. Per WOPI
    /// "preventing token trading" guidance, this token must be fresh and minimum-privilege:
    /// CheckEcosystem has no resource gate, so we bind it to the root container with no
    /// permissions.
    /// </summary>
    /// <remarks>
    /// Sync on purpose — see #363. Splitting the request-building (sync) from the IssueAsync
    /// await (interface call) lets the caller `await accessTokenService.IssueAsync(...)`
    /// directly, which Infer# can see through; an intermediate `private async Task` helper
    /// trips a null-deref FP that no annotation suppresses.
    /// </remarks>
    private WopiAccessTokenRequest BuildEcosystemTokenRequest(string userId) => new()
    {
        UserId = userId,
        UserDisplayName = User.FindFirstValue(ClaimTypes.Name),
        UserEmail = User.FindFirstValue(ClaimTypes.Email),
        ResourceId = storageProvider.RootContainer.Identifier,
        ResourceType = WopiResourceType.Container,
        ContainerPermissions = WopiContainerPermissions.None,
    };

    /// <summary>
    /// Synchronous shaping helper that turns a freshly-issued ecosystem token into the
    /// <see cref="BootstrapInfo"/> the WOPI client expects. Paired with
    /// <see cref="BuildEcosystemTokenRequest"/>; see its remarks for the rationale.
    /// </summary>
    private BootstrapInfo BuildBootstrapInfo(string userId, WopiAccessToken ecosystemToken) => new()
    {
        EcosystemUrl = Url.GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: ecosystemToken.Token),
        UserId = userId,
        SignInName = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
        UserFriendlyName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
    };

    private async Task<WopiAccessToken> IssueFileTokenAsync(IWopiFile file, CancellationToken cancellationToken)
    {
        var perms = await permissionProvider.GetFilePermissionsAsync(User, file, cancellationToken).ConfigureAwait(false);
        return await accessTokenService.IssueAsync(BuildRequest(file.Identifier, WopiResourceType.File) with
        {
            FilePermissions = perms,
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WopiAccessToken> IssueContainerTokenAsync(IWopiFolder container, CancellationToken cancellationToken)
    {
        var perms = await permissionProvider.GetContainerPermissionsAsync(User, container, cancellationToken).ConfigureAwait(false);
        return await accessTokenService.IssueAsync(BuildRequest(container.Identifier, WopiResourceType.Container) with
        {
            ContainerPermissions = perms,
        }, cancellationToken).ConfigureAwait(false);
    }

    private WopiAccessTokenRequest BuildRequest(string resourceId, WopiResourceType resourceType) => new()
    {
        UserId = GetUserIdOrThrow(),
        UserDisplayName = User.FindFirstValue(ClaimTypes.Name),
        UserEmail = User.FindFirstValue(ClaimTypes.Email),
        ResourceId = resourceId,
        ResourceType = resourceType,
    };

    private string GetUserIdOrThrow() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(ClaimTypes.Upn)
        ?? throw new InvalidOperationException("Bootstrap principal lacks an identifier claim.");

    /// <summary>
    /// Parses the <c>X-WOPI-WopiSrc</c> header into a <see cref="WopiResourceType"/> and the
    /// resource identifier. Accepts paths shaped like <c>/wopi/files/{id}</c> or
    /// <c>/wopi/containers/{id}</c>.
    /// </summary>
    internal static bool TryParseWopiSrc(string wopiSrc, out WopiResourceType resourceType, out string resourceId)
    {
        resourceType = default;
        resourceId = string.Empty;

        if (string.IsNullOrWhiteSpace(wopiSrc) ||
            !Uri.TryCreate(wopiSrc, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("files", StringComparison.OrdinalIgnoreCase))
            {
                resourceType = WopiResourceType.File;
                resourceId = Uri.UnescapeDataString(segments[i + 1]);
                return true;
            }
            if (segments[i].Equals("containers", StringComparison.OrdinalIgnoreCase))
            {
                resourceType = WopiResourceType.Container;
                resourceId = Uri.UnescapeDataString(segments[i + 1]);
                return true;
            }
        }
        return false;
    }
}
