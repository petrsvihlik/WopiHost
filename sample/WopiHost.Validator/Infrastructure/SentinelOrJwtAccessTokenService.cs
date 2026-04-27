using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;
using WopiHost.Validator.Models;

namespace WopiHost.Validator.Infrastructure;

/// <summary>
/// Validator-only <see cref="IWopiAccessTokenService"/> that accepts the configured
/// <see cref="WopiOptions.UserId"/> as a sentinel (issuing a valid principal on the fly)
/// in addition to delegating to the real <see cref="JwtAccessTokenService"/>.
/// </summary>
/// <remarks>
/// <para>
/// The Microsoft WOPI validator harness in <c>.github/workflows/wopi-validator.yml</c> is
/// configured (on the master branch) to pass the literal string <c>"Anonymous"</c> as
/// <c>--token</c>. The <c>pull_request_target</c> trigger uses the workflow from master, so
/// until that file is updated we need the host to accept that sentinel — otherwise
/// CheckFileInfo returns 401 on every WOPI test and nothing runs.
/// </para>
/// <para>
/// This decorator is wired up <em>only</em> in the Validator sample; production hosts use the
/// stock <see cref="JwtAccessTokenService"/> via <c>AddWopi()</c> and never see this codepath.
/// </para>
/// </remarks>
public class SentinelOrJwtAccessTokenService(
    JwtAccessTokenService inner,
    IOptionsMonitor<WopiOptions> wopiOptions) : IWopiAccessTokenService
{
    /// <inheritdoc/>
    public Task<WopiAccessToken> IssueAsync(WopiAccessTokenRequest request, CancellationToken cancellationToken = default)
        => inner.IssueAsync(request, cancellationToken);

    /// <inheritdoc/>
    public async Task<WopiAccessTokenValidationResult> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        var sentinel = wopiOptions.CurrentValue.UserId;
        if (!string.IsNullOrEmpty(sentinel) && string.Equals(token, sentinel, StringComparison.Ordinal))
        {
            // Test-harness shortcut: mint a valid in-process principal carrying the same
            // claim layout the real validator would produce, including both file and
            // container permission flags so the validator can roam across endpoints.
            var minted = await inner.IssueAsync(new WopiAccessTokenRequest
            {
                UserId = sentinel,
                UserDisplayName = sentinel,
                ResourceId = sentinel,
                ResourceType = WopiResourceType.File,
                FilePermissions =
                    WopiFilePermissions.UserCanWrite |
                    WopiFilePermissions.UserCanRename |
                    WopiFilePermissions.UserCanAttend |
                    WopiFilePermissions.UserCanPresent,
                ContainerPermissions =
                    WopiContainerPermissions.UserCanCreateChildContainer |
                    WopiContainerPermissions.UserCanCreateChildFile |
                    WopiContainerPermissions.UserCanDelete |
                    WopiContainerPermissions.UserCanRename,
            }, cancellationToken);
            return await inner.ValidateAsync(minted.Token, cancellationToken);
        }
        return await inner.ValidateAsync(token, cancellationToken);
    }
}
