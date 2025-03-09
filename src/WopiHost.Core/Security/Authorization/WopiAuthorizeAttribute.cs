using Microsoft.AspNetCore.Authorization;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Represents an authorization requirement for a given combination of resource, user, and action.
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="WopiAuthorizeAttribute"/> initialized with <paramref name="permission"/>.
/// </remarks>
/// <param name="permission">Permissions required for a given combination of resource, user, and action.</param>
/// <param name="resourceType">type of the resource.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class WopiAuthorizeAttribute(
    Permission permission,
    WopiResourceType resourceType) : AuthorizeAttribute, IWopiAuthorizationRequirement,  IAuthorizationRequirement, IAuthorizationRequirementData
{
    /// <summary>
    /// Gets a permissions required for a given combination of resource, user, and action.
    /// </summary>
    public Permission Permission { get; } = permission;

    /// <summary>
    /// Gets the type of the resource.
    /// </summary>
    public WopiResourceType ResourceType { get; } = resourceType;

    /// <summary>
    /// Gets or sets the identifier of the resource.
    /// </summary>
    public string? ResourceId { get; set; }
    
    /// <summary>
    /// Gets the requirements.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return this;
    }
}