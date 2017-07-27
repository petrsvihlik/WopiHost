using Microsoft.AspNetCore.Authorization;

namespace WopiHost.Abstractions
{
    public class WopiAuthorizationRequirement : IAuthorizationRequirement
    {
        public Permission Permission { get; }

        public WopiAuthorizationRequirement(Permission permission)
        {
            Permission = permission;
        }
    }
}
