using System.Net;
using System.Text.Json;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// End-to-end test for the WOPI permission round-trip — <c>JwtAccessTokenService</c> writes
/// each <see cref="WopiFilePermissions"/> flag into the token's <c>wopi:fperms</c> claim;
/// <c>DefaultWopiPermissionProvider</c> reads them back during <c>CheckFileInfo</c>; the
/// endpoint serializes them as individual <c>UserCan*</c> Boolean response properties.
/// </summary>
/// <remarks>
/// <para>
/// Each side has unit tests; neither tested the full chain. A regression that breaks the
/// contract — claim-name typo, enum reorder, serializer convention change — would silently
/// widen permissions (every flag would either evaluate to <see langword="false"/> in the
/// response, downgrading access, or evaluate to <see langword="true"/>, leaking edit rights
/// to a view-only token). This integration test pins both directions.
/// </para>
/// <para>
/// Source: audit item #456 — "No integration test for permission claim round-trip."
/// </para>
/// </remarks>
[Collection("ReadOnlyEndpoints")]
public sealed class PermissionClaimRoundTripTests(ReadOnlyEndpointsFixture fixture)
{
    private readonly ReadOnlyEndpointsFixture _fixture = fixture;

    [Fact]
    public async Task CheckFileInfo_Surfaces_Exactly_The_Permissions_Baked_Into_The_Token()
    {
        // Pick a mixed permission set so the test would fail loud if the mapping ever lost a
        // flag (any false-negative leaks read access; any false-positive leaks write access):
        // UserCanWrite + UserCanAttend ON, UserCanRename + UserCanPresent OFF.
        const WopiFilePermissions Permissions =
            WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanAttend;

        var token = await _fixture.MintFileTokenAsync(_fixture.FirstFileId, Permissions);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{_fixture.FirstFileId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        // Each WopiFilePermissions flag has a sibling Boolean response property of the same
        // name. The round-trip contract: the token claim drives every one of these properties.
        Assert.True(GetBool(doc, "UserCanWrite"), "UserCanWrite was set on the token but not surfaced on CheckFileInfo — claim-to-response wiring broke.");
        Assert.True(GetBool(doc, "UserCanAttend"), "UserCanAttend was set on the token but not surfaced on CheckFileInfo — claim-to-response wiring broke.");
        Assert.False(GetBool(doc, "UserCanRename"), "UserCanRename was NOT set on the token but appeared on CheckFileInfo — permission widening regression.");
        Assert.False(GetBool(doc, "UserCanPresent"), "UserCanPresent was NOT set on the token but appeared on CheckFileInfo — permission widening regression.");
    }

    [Fact]
    public async Task CheckFileInfo_Surfaces_Only_The_Single_Permission_When_All_Others_Off()
    {
        // Complement of the mixed-set test: a token with EXACTLY UserCanRename — every other
        // flag must come back false. Catches mis-defaulting (a future regression that flipped
        // a flag's default from false → true would slip past unit tests in isolation but be
        // visible here, where the integration runs against the real JwtAccessTokenService +
        // DefaultWopiPermissionProvider + CheckFileInfo builder pipeline).
        //
        // Why NOT use WopiFilePermissions.None for the negative case: JwtAccessTokenService
        // intentionally skips emitting the wopi:fperms claim when permissions equal None
        // (line 72 in JwtAccessTokenService), and the provider then falls back to
        // WopiHostOptions.DefaultFilePermissions — which is non-empty by default. The
        // None-suppression is a separate design decision; this test pins the round-trip
        // for non-None values where the claim IS emitted.
        var token = await _fixture.MintFileTokenAsync(_fixture.FirstFileId, WopiFilePermissions.UserCanRename);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{_fixture.FirstFileId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.True(GetBool(doc, "UserCanRename"), "Single-flag claim must round-trip true.");
        Assert.False(GetBool(doc, "UserCanWrite"), "UserCanWrite was NOT set on the token but appeared on CheckFileInfo — permission widening regression.");
        Assert.False(GetBool(doc, "UserCanAttend"), "UserCanAttend was NOT set on the token but appeared on CheckFileInfo — permission widening regression.");
        Assert.False(GetBool(doc, "UserCanPresent"), "UserCanPresent was NOT set on the token but appeared on CheckFileInfo — permission widening regression.");
    }

    /// <summary>
    /// Defensive: missing the property is the same failure mode as "property is false" from a
    /// claim-routing perspective. Treat absent as <see langword="false"/> rather than letting
    /// the test silently pass when the JSON schema changes.
    /// </summary>
    private static bool GetBool(JsonDocument doc, string propertyName)
        => doc.RootElement.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True;
}
