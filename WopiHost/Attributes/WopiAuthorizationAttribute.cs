using Microsoft.AspNet.Mvc.Filters;
using WopiHost.Abstractions;

namespace WopiHost.Attributes
{
    /// <summary>
    /// Performs authroization based on access token (HTTP parameter).
    /// </summary>
    public class WopiAuthorizationAttribute : AuthorizationFilterAttribute
    {
        public IWopiSecurityHandler SecurityHandler { get; }

        public WopiAuthorizationAttribute(IWopiSecurityHandler securityHandler)
        {
            SecurityHandler = securityHandler;
        }

        public override void OnAuthorization(AuthorizationContext context)
        {
            base.OnAuthorization(context);

            //TODO: implement access_token_ttl https://msdn.microsoft.com/en-us/library/hh695362(v=office.12).aspx

            string fileIdentifier = context.RouteData.Values[Constants.FILE_IDENTIFIER_PARAM].ToString();
	        var accessToken = context.HttpContext.Request.Query[Constants.ACCESS_TOKEN_HTTP_PARAM];
	        if (accessToken.Count == 0)
			{
				accessToken =  context.HttpContext.Request.Headers[Constants.ACCESS_TOKEN_HTTP_PARAM];

			}
			//if (!SecurityHandler.ValidateAccessToken(fileIdentifier, accessToken))
			//{
			//	// If the token validation fails return 'unauthorized'
			//	context.Result = new HttpStatusCodeResult((int)HttpStatusCode.Unauthorized);
			//}
		}
    }
}
