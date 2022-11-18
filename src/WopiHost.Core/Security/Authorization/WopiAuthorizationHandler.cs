using Microsoft.AspNetCore.Authorization;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Performs resource-based authorization.
/// </summary>
public class WopiAuthorizationHandler : AuthorizationHandler<WopiAuthorizationRequirement, FileResource>
{
    /// <summary>
    /// Provides authentication and performs authorization operations for WOPI objects
    /// </summary>
    public IWopiSecurityHandler SecurityHandler { get; }

    /// <summary>
    /// Creates an instance of <see cref="WopiAuthorizationHandler"/>.
    /// </summary>
    /// <param name="securityHandler">AuthNZ handler.</param>
    public WopiAuthorizationHandler(IWopiSecurityHandler securityHandler)
    {
        SecurityHandler = securityHandler;
    }

    /// <summary>
    /// Performs resource-based authorization check.
    /// </summary>
    /// <param name="context">Context of the <see cref="AuthorizationHandler{TRequirement, TResource}"/></param>
    /// <param name="requirement">Security requirement to be fulfilled (e.g. a permission).</param>
    /// <param name="resource">Resource to check the security authorization requirement against.</param>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, WopiAuthorizationRequirement requirement, FileResource resource)
    {
        if (SecurityHandler.IsAuthorized(context.User, resource.FileId, requirement))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
        return Task.CompletedTask;
    }
}
