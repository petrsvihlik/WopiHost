using Microsoft.AspNetCore.Authorization;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents an authorization requirement for a given combination of resource, user, and action.
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="WopiAuthorizationRequirement"/> initialized with <paramref name="permission"/>.
/// </remarks>
/// <param name="permission">Permissions required for a given combination of resource, user, and action.</param>
public class WopiAuthorizationRequirement(Permission permission) : IAuthorizationRequirement
{
    /// <summary>
    /// Gets a permissions required for a given combination of resource, user, and action.
    /// </summary>
    public Permission Permission { get; } = permission;
}
