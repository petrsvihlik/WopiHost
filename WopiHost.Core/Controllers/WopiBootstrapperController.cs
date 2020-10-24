﻿using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Controllers
{
    [Route("wopibootstrapper")]
    public class WopiBootstrapperController : WopiControllerBase
    {
        public WopiBootstrapperController(IWopiStorageProvider fileProvider, IWopiSecurityHandler securityHandler, IOptionsSnapshot<WopiHostOptions> wopiHostOptions)
            : base(fileProvider, securityHandler, wopiHostOptions)
        {

        }

        [HttpPost]
        [Produces("application/json")]
        public IActionResult GetRootContainer()
        {
            var authorizationHeader = HttpContext.Request.Headers["Authorization"];
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
                //TODO: implement WWW-authentication header https://wopirest.readthedocs.io/en/latest/bootstrapper/Bootstrap.html#www-authenticate-header
                var authorizationUri = "https://contoso.com/api/oauth2/authorize";
                var tokenIssuanceUri = "https://contoso.com/api/oauth2/token";
                var providerId = "tp_contoso";
                var urlSchemes = Uri.EscapeDataString("{\"iOS\" : [\"contoso\",\"contoso - EMM\"], \"Android\" : [\"contoso\",\"contoso - EMM\"], \"UWP\": [\"contoso\",\"contoso - EMM\"]}");
                Response.Headers.Add("WWW-Authenticate", $"Bearer authorization_uri=\"{authorizationUri}\",tokenIssuance_uri=\"{tokenIssuanceUri}\",providerId=\"{providerId}\", UrlSchemes=\"{urlSchemes}\"");
                return new UnauthorizedResult();
            }
        }


        private string GetIdFromUrl(string resourceUrl)
        {
            var resourceId = resourceUrl.Substring(resourceUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);
            var queryIndex = resourceId.IndexOf("?", StringComparison.Ordinal);
            if (queryIndex > -1)
            {
                resourceId = resourceId.Substring(0, queryIndex);
            }
            resourceId = Uri.UnescapeDataString(resourceId);
            return resourceId;
        }


        private bool ValidateAuthorizationHeader(StringValues authorizationHeader)
        {
            //TODO: implement header validation http://wopi.readthedocs.io/projects/wopirest/en/latest/bootstrapper/GetRootContainer.html#sample-response
            // http://stackoverflow.com/questions/31948426/oauth-bearer-token-authentication-is-not-passing-signature-validation
            return true;
        }
    }
}
