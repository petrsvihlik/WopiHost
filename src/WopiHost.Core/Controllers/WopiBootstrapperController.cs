using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Controller containing the bootstrap operation.
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="WopiBootstrapperController"/>.
/// </remarks>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="securityHandler">Security handler instance for performing security-related operations.</param>
[Authorize]
[ApiController]
[Route("wopibootstrapper")]
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
public class WopiBootstrapperController(
    IWopiStorageProvider storageProvider,
    IWopiSecurityHandler securityHandler) : ControllerBase
{
    /// <summary>
    /// Gets information about the root container.
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetRootContainer(
        [FromHeader(Name = WopiHeaders.ECOSYSTEM_OPERATION)] string? ecosystemOperation = null,
        [FromHeader(Name = WopiHeaders.WOPI_SRC)] string? wopiSrc = null,
        CancellationToken cancellationToken = default)
    //TODO: fix the path
    {
        var authorizationHeader = HttpContext.Request.Headers.Authorization;
        if (ValidateAuthorizationHeader(authorizationHeader))
        {
            //TODO: supply user
            var user = "Anonymous";

            //TODO: implement bootstrap
            var bootstrapRoot = new BootstrapRootContainerInfo
            {
                Bootstrap = new BootstrapInfo
                {
                    EcosystemUrl = Url.GetWopiSrc(WopiRouteNames.CheckEcosystem),
                    SignInName = "",
                    UserFriendlyName = "",
                    UserId = ""
                }
            };
            if (ecosystemOperation == "GET_ROOT_CONTAINER")
            {
                var resourceId = storageProvider.RootContainerPointer.Identifier;
                var token = await securityHandler.GenerateAccessToken(user, resourceId, cancellationToken);

                bootstrapRoot.RootContainerInfo = new RootContainerInfo
                {
                    ContainerPointer = new ChildContainer(
                        storageProvider.RootContainerPointer.Name,
                        Url.GetWopiSrc(WopiResourceType.Container, storageProvider.RootContainerPointer.Identifier, securityHandler.WriteToken(token)))
                };
            }
            else if (ecosystemOperation == "GET_NEW_ACCESS_TOKEN")
            {
                ArgumentException.ThrowIfNullOrEmpty(wopiSrc);
                var token = await securityHandler.GenerateAccessToken(user, GetIdFromUrl(wopiSrc), cancellationToken);

                bootstrapRoot.AccessTokenInfo = new AccessTokenInfo
                {
                    AccessToken = securityHandler.WriteToken(token),
                    AccessTokenExpiry = token.ValidTo.ToUnixTimestamp()
                };
            }
            else
            {
                return new NotImplementedResult();
            }
            return new JsonResult(bootstrapRoot);
        }
        else
        {
            //TODO: implement WWW-authentication header https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap#www-authenticate-response-header-format
            var authorizationUri = "https://contoso.com/api/oauth2/authorize";
            var tokenIssuanceUri = "https://contoso.com/api/oauth2/token";
            var providerId = "tp_contoso";
            var urlSchemes = Uri.EscapeDataString("{\"iOS\" : [\"contoso\",\"contoso - EMM\"], \"Android\" : [\"contoso\",\"contoso - EMM\"], \"UWP\": [\"contoso\",\"contoso - EMM\"]}");
            Response.Headers.Append("WWW-Authenticate", $"Bearer authorization_uri=\"{authorizationUri}\",tokenIssuance_uri=\"{tokenIssuanceUri}\",providerId=\"{providerId}\", UrlSchemes=\"{urlSchemes}\"");
            return new UnauthorizedResult();
        }
    }

    private static string GetIdFromUrl(string resourceUrl)
    {
        var resourceId = resourceUrl[(resourceUrl.LastIndexOf('/') + 1)..];
        var queryIndex = resourceId.IndexOf('?');
        if (queryIndex > -1)
        {
            resourceId = resourceId[..queryIndex];
        }
        resourceId = Uri.UnescapeDataString(resourceId);
        return resourceId;
    }

    private static bool ValidateAuthorizationHeader(StringValues authorizationHeader)
    {
        //TODO: implement header validation https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer#sample-response
        // http://stackoverflow.com/questions/31948426/oauth-bearer-token-authentication-is-not-passing-signature-validation
        return true;
    }
}
