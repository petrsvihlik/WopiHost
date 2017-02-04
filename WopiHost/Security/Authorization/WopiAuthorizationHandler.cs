using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using WopiHost.Abstractions;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace WopiHost.Authorization
{
	/// <summary>
	/// Performs resource-based authorization.
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
			if (SecurityHandler.IsAuthorized(context.User, resource.FileId, requirement))
			{
				context.Succeed(requirement);
			}
			else
			{
				context.Fail();
			}
			return Task.CompletedTask;
		}
	}
}
