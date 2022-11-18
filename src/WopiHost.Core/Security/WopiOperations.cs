using WopiHost.Abstractions;

namespace WopiHost.Core.Security;

/// <summary>
/// Operations to be used with resource-based authorization.
/// </summary>
public static class WopiOperations
{
    /// <summary>
    /// Given operation requires the user to have a create file permission.
    /// </summary>
    public static readonly WopiAuthorizationRequirement Create = new(Permission.Create);

    /// <summary>
    /// Given operation requires the user to have a read file permission.
    /// </summary>
    public static readonly WopiAuthorizationRequirement Read = new(Permission.Read);

    /// <summary>
    /// Given operation requires the user to have a update file permission.
    /// </summary>
    public static readonly WopiAuthorizationRequirement Update = new(Permission.Update);

    /// <summary>
    /// Given operation requires the user to have a delete file permission.
    /// </summary>
    public static readonly WopiAuthorizationRequirement Delete = new(Permission.Delete);
}