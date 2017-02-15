using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace WopiHost.Core.Security
{
	public class WopiOperations
	{
		//TODO: base on WopiAction 
		public static OperationAuthorizationRequirement Create = new OperationAuthorizationRequirement { Name = "Create" };
		public static OperationAuthorizationRequirement Read = new OperationAuthorizationRequirement { Name = "Read" };
		public static OperationAuthorizationRequirement Update = new OperationAuthorizationRequirement { Name = "Update" };
		public static OperationAuthorizationRequirement Delete = new OperationAuthorizationRequirement { Name = "Delete" };
	}
}