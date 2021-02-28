using WopiHost.Abstractions;

namespace WopiHost.Core.Security
{
    public static class WopiOperations
    {
        public static readonly WopiAuthorizationRequirement Create = new(Permission.Create);
        public static readonly WopiAuthorizationRequirement Read = new(Permission.Read);
        public static readonly WopiAuthorizationRequirement Update = new(Permission.Update);
        public static readonly WopiAuthorizationRequirement Delete = new(Permission.Delete);
    }
}