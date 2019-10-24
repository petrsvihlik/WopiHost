using WopiHost.Abstractions;

namespace WopiHost.Core.Security
{
    public static class WopiOperations
    {
        public static readonly WopiAuthorizationRequirement Create = new WopiAuthorizationRequirement(Permission.Create);
        public static readonly WopiAuthorizationRequirement Read = new WopiAuthorizationRequirement(Permission.Read);
        public static readonly WopiAuthorizationRequirement Update = new WopiAuthorizationRequirement(Permission.Update);
        public static readonly WopiAuthorizationRequirement Delete = new WopiAuthorizationRequirement(Permission.Delete);
    }
}