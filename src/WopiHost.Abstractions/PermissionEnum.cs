namespace WopiHost.Abstractions;

/// <summary>
/// Permissions used for authorization requirements
/// </summary>
[Flags]
public enum Permission
{
    /// <summary>
    /// User has no permissions
    /// </summary>
    None = 0,

    /// <summary>
    /// User can create files
    /// </summary>
    Create = 1,

    /// <summary>
    /// User can view files
    /// </summary>
    Read = 2,

    /// <summary>
    /// User can modify files
    /// </summary>
    Update = 4,

    /// <summary>
    /// User can delete files
    /// </summary>
    Delete = 8
}
