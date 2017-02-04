using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using WopiHost.Abstractions;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace WopiHost.Authorization
{
	/// <summary>
	/// Performs authorization based on access token (HTTP parameter).
	/// </summary>
	public class WopiAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, FileResource>
	{
		public IWopiSecurityHandler SecurityHandler { get; }

		public WopiAuthorizationHandler(IWopiSecurityHandler securityHandler)
		{
			SecurityHandler = securityHandler;
		}

		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OperationAuthorizationRequirement requirement, FileResource resource)
		{
			//TODO: implement access_token_ttl https://msdn.microsoft.com/en-us/library/hh695362(v=office.12).aspx		
			//context.User
			//context.
			//if (requirement == WopiOperations.Edit) //TODO:base on WopiOperations
			{
				//if (SecurityHandler.ValidateAccessToken(resource.FileId, resource.Token))
				{
					context.Succeed(requirement);
				}
			}
			return Task.CompletedTask;
		}
	}
}
