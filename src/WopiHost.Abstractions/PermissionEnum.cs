namespace WopiHost.Abstractions;

/// <summary>
/// Specifies the permission an endpoint's authorization requirement demands.
/// </summary>
public enum Permission
{
    /// <summary>
    /// No specific permission.
    /// </summary>
    None,

    /// <summary>
    /// Permission to create a container or file.
    /// </summary>
    Create,

    /// <summary>
    /// Permission to create a child file in a container.
    /// </summary>
    CreateChildFile,

    /// <summary>
    /// Permission to view a container or file.
    /// </summary>
    Read,

    /// <summary>
    /// Permission to modify a container or file.
    /// </summary>
    Update,

    /// <summary>
    /// Permission to rename a container or file.
    /// </summary>
    Rename,

    /// <summary>
    /// Permission to delete a container or file.
    /// </summary>
    Delete
}
