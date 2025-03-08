namespace WopiHost.Abstractions;

/// <summary>
/// Permissions used for authorization requirements
/// </summary>
public enum Permission
{
    /// <summary>
    /// User has no permissions
    /// </summary>
    None,

    /// <summary>
    /// User can create container/file
    /// </summary>
    Create,

    /// <summary>
    /// User can create child file in container
    /// </summary>
    CreateChildFile,

    /// <summary>
    /// User can view container/file
    /// </summary>
    Read,

    /// <summary>
    /// User can modify container/file
    /// </summary>
    Update,

    /// <summary>
    /// User can rename container/file
    /// </summary>
    Rename,

    /// <summary>
    /// User can delete container/file
    /// </summary>
    Delete
}
