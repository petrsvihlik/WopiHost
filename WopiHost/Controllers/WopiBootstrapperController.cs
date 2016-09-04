using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using WopiHost.Abstractions;
using WopiHost.Models;
using WopiHost.Results;

namespace WopiHost.Controllers
{
	[Route("wopibootstrapper")]
	public class WopiBootstrapperController : WopiControllerBase
	{
		public WopiBootstrapperController(IWopiFileProvider fileProvider, IWopiSecurityHandler securityHandler, IConfiguration configuration) : base(fileProvider, securityHandler, configuration)
		{

		}

		[HttpPost]
		[Produces("application/json")]
		public IActionResult GetRootContainer()
		{
			if (HttpContext.Request.Headers["X-WOPI-EcosystemOperation"] == "GET_ROOT_CONTAINER")
			{
				var authorizationHeader = HttpContext.Request.Headers["Authorization"];
				if (ValidateAuthorizationHeader(authorizationHeader))
				{
					var root = FileProvider.GetWopiContainer(@".\");
					//TODO: implement bootstrap + token
					BootstrapRootContainerInfo bootstrapRoot = new BootstrapRootContainerInfo
					{
						Bootstrap = new BootstrapInfo
						{
							EcosystemUrl = "",
							SignInName = "",
							UserFriendlyName = "",
							UserId = ""
						},
						RootContainerInfo = new RootContainerInfo
						{
							ContainerPointer = new ChildContainer
							{
								Name = root.Name,
								Url = UrlGenerator.GetContainerUrl(root.Identifier, "1")
							}
						}
					};
					return new JsonResult(bootstrapRoot);
				}
				else
				{
					//TODO: implement WWW-authentication header https://wopirest.readthedocs.io/en/latest/bootstrapper/Bootstrap.html#www-authenticate-header
					string authorizationUri = "https://contoso.com/api/oauth2/authorize";
					string tokenIssuanceUri = "https://contoso.com/api/oauth2/token";
					string providerId = "tp_contoso";
					string urlSchemes = Uri.EscapeDataString("{\"iOS\" : [\"contoso\",\"contoso - EMM\"], \"Android\" : [\"contoso\",\"contoso - EMM\"], \"UWP\": [\"contoso\",\"contoso - EMM\"]}");
					Response.Headers.Add("WWW-Authenticate", $"Bearer authorization_uri=\"{authorizationUri}\",tokenIssuance_uri=\"{tokenIssuanceUri}\",providerId=\"{providerId}\", UrlSchemes=\"{urlSchemes}\"");
					return new UnauthorizedResult();
				}
			}
			else
			{
				return new NotImplementedResult();
			}
		}

		private bool ValidateAuthorizationHeader(StringValues authorizationHeader)
		{
			//TODO: implement header validation http://wopi.readthedocs.io/projects/wopirest/en/latest/bootstrapper/GetRootContainer.html#sample-response
			http://stackoverflow.com/questions/31948426/oauth-bearer-token-authentication-is-not-passing-signature-validation
			return true;
		}
	}
}
