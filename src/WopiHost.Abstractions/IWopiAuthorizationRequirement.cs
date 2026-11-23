namespace WopiHost.Abstractions;

/// <summary>
/// Specifies the kind of WOPI resource.
/// </summary>
public enum WopiResourceType
{
    /// <summary>
    /// A file (<see cref="IWopiFile"/>).
    /// </summary>
    File,
    /// <summary>
    /// A container (<see cref="IWopiContainer"/>).
    /// </summary>
    Container
}

/// <summary>
/// Declares the resource type and permission an authenticated caller must hold to invoke a
/// given endpoint.
/// </summary>
/// <remarks>
/// Implementations (typically <c>WopiAuthorizeAttribute</c>) are attached to the endpoint's
/// authorization policy once and shared across requests, so they must be immutable and free of
/// per-request state. The per-request resource id is <em>not</em> part of this requirement: the
/// authorization handler reads it from <c>HttpContext.Request.RouteValues["id"]</c>. Storing
/// per-request state on the shared requirement races across requests.
/// </remarks>
public interface IWopiAuthorizationRequirement
{
    /// <summary>
    /// Gets the permission the caller must hold on the resource.
    /// </summary>
    Permission Permission { get; }

    /// <summary>
    /// Gets the type of the resource the permission applies to.
    /// </summary>
    WopiResourceType ResourceType { get; }
}
