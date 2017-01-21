using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using WopiHost.Abstractions;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace WopiHost.Authorization
{
	public class WopiOperations
	{
		//TODO: base on WopiAction 
		public static OperationAuthorizationRequirement Create = new OperationAuthorizationRequirement { Name = "Create" };
		public static OperationAuthorizationRequirement Read = new OperationAuthorizationRequirement { Name = "Read" };
		public static OperationAuthorizationRequirement Update = new OperationAuthorizationRequirement { Name = "Update" };
		public static OperationAuthorizationRequirement Delete = new OperationAuthorizationRequirement { Name = "Delete" };
	}

	/// <summary>
	/// Performs authorization based on access token (HTTP parameter).
	/// </summary>
	public class WopiAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, TokenContainer>
	{
		public IWopiSecurityHandler SecurityHandler { get; }

		public WopiAuthorizationHandler(IWopiSecurityHandler securityHandler)
		{
			SecurityHandler = securityHandler;
		}

		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OperationAuthorizationRequirement requirement, TokenContainer resource)
		{
			//TODO: implement access_token_ttl https://msdn.microsoft.com/en-us/library/hh695362(v=office.12).aspx		
			//if (requirement == WopiOperations.Edit) //TODO:base on WopiOperations
			{
				if (SecurityHandler.ValidateAccessToken(resource.FileId, resource.Token))
				{
					context.Succeed(requirement);
				}
			}
			return Task.CompletedTask;
		}
	}
}
