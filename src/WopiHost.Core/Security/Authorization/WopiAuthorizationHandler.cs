using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Performs resource-based authorization.
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="WopiAuthorizationHandler"/>.
/// </remarks>
/// <param name="securityHandler">AuthNZ handler.</param>
public class WopiAuthorizationHandler(IWopiSecurityHandler securityHandler)
    : AuthorizationHandler<WopiAuthorizeAttribute, HttpContext>
{
    /// <summary>
    /// Performs resource-based authorization check.
    /// </summary>
    /// <param name="context">Context of the <see cref="AuthorizationHandler{TRequirement, TResource}"/></param>
    /// <param name="requirement">Security requirement to be fulfilled (a permission on which resource).</param>
    /// <param name="resource">httpContext resource</param>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, WopiAuthorizeAttribute requirement, HttpContext resource)
    {
        // try to retrieve resource identifier from the route
        if (resource.Request.RouteValues.TryGetValue("id", out var fileIdRaw) &&
            fileIdRaw is not null)
        {
            requirement.ResourceId = fileIdRaw.ToString();
        }

        // check additional permissions
        foreach (var checkPermission in requirement.CheckPermissions)
        {
            var checkRequirement = new WopiAuthorizeAttribute(requirement.ResourceType, checkPermission)
            {
                ResourceId = requirement.ResourceId
            };
            resource.Items.Add(checkPermission, await securityHandler.IsAuthorized(context.User, checkRequirement));
        }

        // check if the user is authorized
        if (await securityHandler.IsAuthorized(context.User, requirement))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}
