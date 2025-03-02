﻿using Microsoft.AspNetCore.Authorization;
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
    : AuthorizationHandler<WopiAuthorizationRequirement, FileResource>
{
    /// <summary>
    /// Performs resource-based authorization check.
    /// </summary>
    /// <param name="context">Context of the <see cref="AuthorizationHandler{TRequirement, TResource}"/></param>
    /// <param name="requirement">Security requirement to be fulfilled (e.g. a permission).</param>
    /// <param name="resource">Resource to check the security authorization requirement against.</param>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, WopiAuthorizationRequirement requirement, FileResource resource)
    {
        if (await securityHandler.IsAuthorized(context.User, resource.FileId, requirement))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}
