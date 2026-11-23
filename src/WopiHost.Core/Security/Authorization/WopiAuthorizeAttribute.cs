using Microsoft.AspNetCore.Authorization;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Represents a WOPI authorization requirement: the permission a caller must hold on a resource of
/// a given type to invoke an endpoint.
/// </summary>
/// <param name="resourceType">The type of the resource the permission applies to.</param>
/// <param name="permission">The permission the caller must hold on the resource.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class WopiAuthorizeAttribute(
    WopiResourceType resourceType,
    Permission permission) : AuthorizeAttribute, IWopiAuthorizationRequirement, IAuthorizationRequirement, IAuthorizationRequirementData
{
    /// <inheritdoc/>
    public Permission Permission { get; } = permission;

    /// <inheritdoc/>
    public WopiResourceType ResourceType { get; } = resourceType;

    /// <inheritdoc/>
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return this;
    }
}