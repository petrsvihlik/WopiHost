namespace WopiHost.Abstractions;

/// <summary>
/// The Wopi resource type
/// </summary>
public enum WopiResourceType
{
    /// <summary>
    /// <see cref="IWopiFile"/>
    /// </summary>
    File,
    /// <summary>
    /// <see cref="IWopiFolder"/>
    /// </summary>
    Container
}


/// <summary>
/// Represents an authorization requirement for a given combination of resource, user, and action.
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="IWopiAuthorizationRequirement"/> initialized
/// </remarks>
public interface IWopiAuthorizationRequirement
{
    /// <summary>
    /// Gets a permissions required for a given combination of resource, user, and action.
    /// </summary>
    Permission Permission { get; }

    /// <summary>
    /// Gets the type of the resource.
    /// </summary>
    WopiResourceType ResourceType { get; }

    /// <summary>
    /// Gets or sets the identifier of the resource.
    /// </summary>
    string? ResourceId { get; set; }
}
