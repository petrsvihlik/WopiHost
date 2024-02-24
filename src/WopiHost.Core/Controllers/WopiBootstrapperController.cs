using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Controller containing the bootstrap operation.
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="WopiBootstrapperController"/>.
/// </remarks>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="securityHandler">Security handler instance for performing security-related operations.</param>
/// <param name="wopiHostOptions">WOPI Host configuration</param>
[Route("wopibootstrapper")]
public class WopiBootstrapperController(IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler, IOptionsSnapshot<WopiHostOptions> wopiHostOptions) : WopiControllerBase(storageProvider, securityHandler, wopiHostOptions)
{

    /// <summary>
    /// Gets information about the root container.
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Produces(MediaTypeNames.Application.Json)]
    public IActionResult GetRootContainer() //TODO: fix the path
    {
        var authorizationHeader = HttpContext.Request.Headers.Authorization;
        var ecosystemOperation = HttpContext.Request.Headers[WopiHeaders.ECOSYSTEM_OPERATION];
        var wopiSrc = HttpContext.Request.Headers[WopiHeaders.WOPI_SRC].FirstOrDefault();

        if (ValidateAuthorizationHeader(authorizationHeader))
        {
            //TODO: supply user
            var user = "Anonymous";

            //TODO: implement bootstrap
            var bootstrapRoot = new BootstrapRootContainerInfo
            {
                Bootstrap = new BootstrapInfo
                {
                    EcosystemUrl = GetWopiUrl("ecosystem", accessToken: "TODO"),
                    SignInName = "",
                    UserFriendlyName = "",
                    UserId = ""
                }
            };
            if (ecosystemOperation == "GET_ROOT_CONTAINER")
            {
                var resourceId = StorageProvider.RootContainerPointer.Identifier;
                var token = SecurityHandler.GenerateAccessToken(user, resourceId);

                bootstrapRoot.RootContainerInfo = new RootContainerInfo
                {
                    ContainerPointer = new ChildContainer
                    {
                        Name = StorageProvider.RootContainerPointer.Name,
                        Url = GetWopiUrl("containers", resourceId, SecurityHandler.WriteToken(token))
                    }
                };
            }
            else if (ecosystemOperation == "GET_NEW_ACCESS_TOKEN")
            {
                var token = SecurityHandler.GenerateAccessToken(user, GetIdFromUrl(wopiSrc));

                bootstrapRoot.AccessTokenInfo = new AccessTokenInfo
                {
                    AccessToken = SecurityHandler.WriteToken(token),
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
            //TODO: implement WWW-authentication header https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap#www-authenticate-response-header-format
            var authorizationUri = "https://contoso.com/api/oauth2/authorize";
            var tokenIssuanceUri = "https://contoso.com/api/oauth2/token";
            var providerId = "tp_contoso";
            var urlSchemes = Uri.EscapeDataString("{\"iOS\" : [\"contoso\",\"contoso - EMM\"], \"Android\" : [\"contoso\",\"contoso - EMM\"], \"UWP\": [\"contoso\",\"contoso - EMM\"]}");
            Response.Headers.Append("WWW-Authenticate", $"Bearer authorization_uri=\"{authorizationUri}\",tokenIssuance_uri=\"{tokenIssuanceUri}\",providerId=\"{providerId}\", UrlSchemes=\"{urlSchemes}\"");
            return new UnauthorizedResult();
        }
    }

    private string GetIdFromUrl(string resourceUrl)
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

    private bool ValidateAuthorizationHeader(StringValues authorizationHeader)
    {
        //TODO: implement header validation https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer#sample-response
        // http://stackoverflow.com/questions/31948426/oauth-bearer-token-authentication-is-not-passing-signature-validation
        return true;
    }
}
