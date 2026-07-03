using System.IdentityModel.Tokens.Jwt;
using WopiHost.Abstractions;
using WopiHost.Discovery.Enumerations;
using WopiHost.Web.Services;
using Xunit;

namespace WopiHost.SmokeTests;

/// <summary>
/// Covers the anonymous sample's view/edit token scoping
/// (<see cref="WopiAccessTokenMinter"/> in <c>sample/WopiHost.Web</c>): a token minted for a
/// non-Edit action must not carry write/rename permissions. Collabora Online uses a single
/// editor URL for both modes and derives view-vs-edit purely from CheckFileInfo permission
/// flags, so an over-permissive token silently opens a "view" link in edit mode with no
/// visible symptom in the URL. Pure-unit; no Playwright/browser required.
/// </summary>
public class WebSampleTokenScopingTests
{
    private static readonly JwtSecurityTokenHandler s_handler = new();

    private static WopiFilePermissions MintedPermissions(WopiActionEnum action)
    {
        var minter = new WopiAccessTokenMinter();
        var (token, _) = minter.Mint("user-1", "file-42", action);
        var jwt = s_handler.ReadJwtToken(token);
        return Enum.Parse<WopiFilePermissions>(jwt.Claims.First(c => c.Type == WopiClaimTypes.FilePermissions).Value);
    }

    [Fact]
    public void Mint_EditAction_GrantsWriteAndRename()
    {
        var perms = MintedPermissions(WopiActionEnum.Edit);

        Assert.True(perms.HasFlag(WopiFilePermissions.UserCanWrite));
        Assert.True(perms.HasFlag(WopiFilePermissions.UserCanRename));
    }

    [Theory]
    [InlineData(WopiActionEnum.View)]
    [InlineData(WopiActionEnum.EmbedView)]
    [InlineData(WopiActionEnum.MobileView)]
    public void Mint_NonEditAction_OmitsWriteAndRename(WopiActionEnum action)
    {
        var perms = MintedPermissions(action);

        Assert.False(perms.HasFlag(WopiFilePermissions.UserCanWrite));
        Assert.False(perms.HasFlag(WopiFilePermissions.UserCanRename));
        // Interaction-only flags survive the strip — they don't grant content mutation.
        Assert.True(perms.HasFlag(WopiFilePermissions.UserCanAttend));
    }
}
