using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Extends the <see cref="ControllerBase"/> with some basic WOPI-related functionality.
/// </summary>
public abstract class WopiControllerBase : ControllerBase
{
    /// <summary>
    /// Provides access to the storage.
    /// </summary>
    protected IWopiStorageProvider StorageProvider { get; }

    /// <summary>
    /// Provides security-related actions.
    /// </summary>
    protected IWopiSecurityHandler SecurityHandler { get; }

    /// <summary>
    /// WOPI Host configuration object.
    /// </summary>
    protected IOptionsSnapshot<WopiHostOptions> WopiHostOptions { get; }

    /// <summary>
    /// WOPI Host base URL
    /// </summary>
    public Uri BaseUrl => new(HttpContext.Request.Scheme + "://" + HttpContext.Request.Host);

    /// <summary>
    /// WOPI authentication token
    /// </summary>
    protected string AccessToken
    {
        get
        {
            //TODO: an alternative would be HttpContext.GetTokenAsync(AccessTokenDefaults.AuthenticationScheme, AccessTokenDefaults.AccessTokenQueryName).Result (if the code below doesn't work)
            var authenticateInfo = HttpContext.AuthenticateAsync(AccessTokenDefaults.AUTHENTICATION_SCHEME).Result;
            return authenticateInfo?.Properties?.GetTokenValue(AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME);
        }
    }

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="storageProvider">Object facilitating access to the storage of WOPI files.</param>
    /// <param name="securityHandler">Object facilitating security-related actions.</param>
    /// <param name="wopiHostOptions">WOPI Host configuration object</param>
    protected WopiControllerBase(IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler, IOptionsSnapshot<WopiHostOptions> wopiHostOptions)
    {
        StorageProvider = storageProvider;
        SecurityHandler = securityHandler;
        WopiHostOptions = wopiHostOptions;
    }

    /// <summary>
    /// Creates a simple URL to access a WOPI object of choice.
    /// </summary>
    /// <param name="controller">Controller to be called.</param>
    /// <param name="identifier">Identifier of an object associated to the controller.</param>
    /// <param name="accessToken">Access token to use for authentication for the given controller.</param>
    /// <returns></returns>
    protected Uri GetWopiUrl(string controller, string identifier = null, string accessToken = null)
    {
        identifier = identifier is null ? "" : "/" + Uri.EscapeDataString(identifier);
        accessToken = Uri.EscapeDataString(accessToken);
        return new Uri(BaseUrl, $"/wopi/{controller}{identifier}?access_token={accessToken}");
    }
}
