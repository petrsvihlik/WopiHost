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
    /// <see cref="IWopiContainer"/>
    /// </summary>
    Container
}

/// <summary>
/// Static policy declaration: the resource-type and permission combination an authenticated
/// caller must hold to invoke a given endpoint. Implementations live as MVC attributes
/// (typically <c>WopiAuthorizeAttribute</c>) and are cached on the action descriptor — they
/// must therefore be immutable and free of any per-request state.
/// </summary>
/// <remarks>
/// The per-request resource id is <em>not</em> part of this requirement. It belongs to the
/// authorization handler, which derives it from <c>HttpContext.Request.RouteValues["id"]</c>
/// when needed. Mixing per-request state into the (shared) requirement was the cause of the
/// race fixed alongside this contract — see #380 items 2.5 and 5.3.
/// </remarks>
public interface IWopiAuthorizationRequirement
{
    /// <summary>
    /// Gets the permission required for a given combination of resource, user, and action.
    /// </summary>
    Permission Permission { get; }

    /// <summary>
    /// Gets the type of the resource.
    /// </summary>
    WopiResourceType ResourceType { get; }
}
