using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Authorization handler for <see cref="WopiAuthorizeAttribute"/>. Enforces that the access
/// token grants the <see cref="WopiAuthorizeAttribute.Permission"/> required by the endpoint
/// (mapped to the file/container permission flags the token carries).
/// </summary>
/// <remarks>
/// <para>
/// We intentionally do not enforce a strict route-id ↔ <see cref="WopiClaimTypes.ResourceId"/>
/// match. WOPI clients (including Office for the web) and the Microsoft WOPI validator use a
/// single token to navigate from a file to its ancestor container, list siblings, etc. — so a
/// strict per-resource binding would break the protocol's assumed cross-resource access.
/// The <see cref="WopiClaimTypes.ResourceId"/> claim is still written into the token for
/// audit/logging and is logged on mismatch so hosts can trace unexpected reuse.
/// </para>
/// <para>
/// If you need stricter per-resource enforcement (e.g. compliance), register an additional
/// <see cref="IAuthorizationHandler"/> that compares the route id with the claim and fails
/// the requirement on mismatch.
/// </para>
/// </remarks>
public partial class WopiAuthorizationHandler(ILogger<WopiAuthorizationHandler> logger)
    : AuthorizationHandler<WopiAuthorizeAttribute, HttpContext>
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Token bound to resource '{TokenRid}' is being used against route id '{RouteId}'. " +
            "This is allowed by default (WOPI tokens are session-scoped); register a stricter IAuthorizationHandler if you need to block cross-resource reuse.")]
    private static partial void LogResourceBindingMismatch(ILogger logger, string tokenRid, string? routeId);

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

        WarnIfResourceBindingMismatch(user, requirement);

        if (!HasRequiredPermission(user, requirement))
        {
            context.Fail(new AuthorizationFailureReason(this, $"Token lacks {requirement.Permission} permission on this resource."));
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private void WarnIfResourceBindingMismatch(ClaimsPrincipal user, WopiAuthorizeAttribute requirement)
    {
        if (string.IsNullOrEmpty(requirement.ResourceId)) return;
        var ridClaim = user.FindFirstValue(WopiClaimTypes.ResourceId);
        if (!string.IsNullOrEmpty(ridClaim) && !string.Equals(ridClaim, requirement.ResourceId, StringComparison.Ordinal))
        {
            LogResourceBindingMismatch(logger, ridClaim, requirement.ResourceId);
        }
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
