using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Controllers
{
    public abstract class WopiControllerBase : ControllerBase
    {
        protected IWopiStorageProvider StorageProvider { get; set; }

        protected IWopiSecurityHandler SecurityHandler { get; set; }

        public string BaseUrl => HttpContext.Request.Scheme + "://" + HttpContext.Request.Host;

        protected string AccessToken
        {
            get
            {
                //TODO: an alternative would be HttpContext.GetTokenAsync(AccessTokenDefaults.AuthenticationScheme, AccessTokenDefaults.AccessTokenQueryName).Result (if the code below doesn't work)
                var authenticateInfo = HttpContext.AuthenticateAsync(AccessTokenDefaults.AuthenticationScheme).Result;
                return authenticateInfo?.Properties?.GetTokenValue(AccessTokenDefaults.AccessTokenQueryName);
            }
        }

        protected WopiControllerBase(IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler, IOptionsSnapshot<WopiHostOptions> wopiHostOptions)
        {
            StorageProvider = storageProvider;
            SecurityHandler = securityHandler;
        }

        protected string GetWopiUrl(string controller, string identifier = null, string accessToken = null)
        {
            identifier = identifier == null ? "" : "/" + Uri.EscapeDataString(identifier);
            accessToken = Uri.EscapeDataString(accessToken);
            return $"{BaseUrl}/wopi/{controller}{identifier}?access_token={accessToken}";
        }
    }
}
