using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Authorization handler for <see cref="WopiAuthorizeAttribute"/>. Performs two checks:
/// <list type="number">
///   <item><description>
///     <strong>Resource binding</strong>: the route's <c>{id}</c> must match the
///     <see cref="WopiClaimTypes.ResourceId"/> claim on the access token. This is what
///     stops a token issued for file A being replayed against file B.
///   </description></item>
///   <item><description>
///     <strong>Permission</strong>: the <see cref="WopiAuthorizeAttribute.Permission"/> required
///     by the endpoint must be granted by the file/container permission flags baked into the
///     access token at issuance time.
///   </description></item>
/// </list>
/// </summary>
public class WopiAuthorizationHandler(ILogger<WopiAuthorizationHandler> logger)
    : AuthorizationHandler<WopiAuthorizeAttribute, HttpContext>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, WopiAuthorizeAttribute requirement, HttpContext resource)
    {
        if (resource.Request.RouteValues.TryGetValue("id", out var fileIdRaw) && fileIdRaw is not null)
        {
            requirement.ResourceId = fileIdRaw.ToString();
        }

        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Fail(new AuthorizationFailureReason(this, "User is not authenticated."));
            return Task.CompletedTask;
        }

        if (!IsTokenBoundToRequestedResource(user, requirement))
        {
            logger.LogWarning("Token resource binding mismatch (route id '{RouteId}' vs token rid '{TokenRid}').",
                requirement.ResourceId, user.FindFirstValue(WopiClaimTypes.ResourceId));
            context.Fail(new AuthorizationFailureReason(this, "Access token is not bound to the requested resource."));
            return Task.CompletedTask;
        }

        if (!HasRequiredPermission(user, requirement))
        {
            context.Fail(new AuthorizationFailureReason(this, $"Token lacks {requirement.Permission} permission on this resource."));
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static bool IsTokenBoundToRequestedResource(ClaimsPrincipal user, WopiAuthorizeAttribute requirement)
    {
        // No id on the route (e.g. ecosystem/bootstrap-style endpoints) — nothing to bind to.
        if (string.IsNullOrEmpty(requirement.ResourceId))
        {
            return true;
        }

        var ridClaim = user.FindFirstValue(WopiClaimTypes.ResourceId);
        return string.Equals(ridClaim, requirement.ResourceId, StringComparison.Ordinal);
    }

    private static bool HasRequiredPermission(ClaimsPrincipal user, WopiAuthorizeAttribute requirement)
    {
        return requirement.ResourceType switch
        {
            WopiResourceType.File => HasFilePermission(user, requirement.Permission),
            WopiResourceType.Container => HasContainerPermission(user, requirement.Permission),
            _ => false,
        };
    }

    private static bool HasFilePermission(ClaimsPrincipal user, Permission required)
    {
        var perms = ReadFlags<WopiFilePermissions>(user, WopiClaimTypes.FilePermissions);

        // Read is implied by holding a valid, resource-bound token.
        if (required is Permission.Read or Permission.None)
        {
            return true;
        }

        // Edits are blocked when the token explicitly says read-only.
        if (perms.HasFlag(WopiFilePermissions.ReadOnly))
        {
            return false;
        }

        return required switch
        {
            Permission.Update => perms.HasFlag(WopiFilePermissions.UserCanWrite),
            Permission.Rename => perms.HasFlag(WopiFilePermissions.UserCanRename),
            // No file-level WopiFilePermissions flag for delete; gate on UserCanWrite.
            Permission.Delete => perms.HasFlag(WopiFilePermissions.UserCanWrite),
            // PutRelativeFile / Create at the file scope is gated by UserCanNotWriteRelative.
            Permission.Create or Permission.CreateChildFile => !perms.HasFlag(WopiFilePermissions.UserCanNotWriteRelative),
            _ => false,
        };
    }

    private static bool HasContainerPermission(ClaimsPrincipal user, Permission required)
    {
        var perms = ReadFlags<WopiContainerPermissions>(user, WopiClaimTypes.ContainerPermissions);
        return required switch
        {
            Permission.Read or Permission.None => true,
            Permission.Create => perms.HasFlag(WopiContainerPermissions.UserCanCreateChildContainer),
            Permission.CreateChildFile => perms.HasFlag(WopiContainerPermissions.UserCanCreateChildFile),
            Permission.Delete => perms.HasFlag(WopiContainerPermissions.UserCanDelete),
            Permission.Rename => perms.HasFlag(WopiContainerPermissions.UserCanRename),
            _ => false,
        };
    }

    private static T ReadFlags<T>(ClaimsPrincipal user, string claimType) where T : struct, Enum
    {
        var raw = user.FindFirstValue(claimType);
        return !string.IsNullOrEmpty(raw) && Enum.TryParse<T>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : default;
    }
}
