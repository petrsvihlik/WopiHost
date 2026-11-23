using WopiHost.Core.Security;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Provides the in-memory configuration shared by <see cref="OidcWebAppFactory"/>
/// (real OIDC handshake) and <see cref="TestAuthOidcWebAppFactory"/> (<see cref="TestAuthHandler"/> stand-in).
/// </summary>
internal static class OidcSampleTestConfig
{
    /// <summary>The client id the sample registers with the IdP. Mock-oauth2-server accepts any id.</summary>
    public const string TestClientId = "wopihost-oidc-sample";

    /// <summary>The client secret. Mock-oauth2-server accepts any value.</summary>
    public const string TestClientSecret = "test-client-secret";

    /// <summary>
    /// Builds the in-memory configuration applied to the OIDC sample web host.
    /// </summary>
    /// <param name="oidcAuthority">The OIDC authority URL of the mock identity provider.</param>
    /// <param name="wopiSigningSecret">The shared secret the WOPI access tokens are signed with.</param>
    /// <param name="wopiBackendUrl">The base URL of the WOPI backend.</param>
    /// <returns>The configuration key/value pairs.</returns>
    public static Dictionary<string, string?> Build(string oidcAuthority, string wopiSigningSecret, string wopiBackendUrl) => new()
    {
        ["Wopi:HostUrl"] = wopiBackendUrl,
        ["Wopi:ClientUrl"] = "https://office.example.test",
        ["Wopi:Discovery:NetZone"] = "ExternalHttps",
        ["Wopi:Discovery:RefreshInterval"] = "12:00:00",
        ["Wopi:StorageProvider:RootPath"] = TestPaths.WopiDocsRoot,
        [$"{WopiSecurityOptions.SectionName}:{nameof(WopiSecurityOptions.SigningKey)}"] = Convert.ToBase64String(SigningKeyBytes(wopiSigningSecret)),
        ["Oidc:Authority"] = oidcAuthority,
        ["Oidc:ClientId"] = TestClientId,
        ["Oidc:ClientSecret"] = TestClientSecret,
        ["Oidc:RequireHttpsMetadata"] = "false",
        ["Oidc:UsePkce"] = "true",
        ["Oidc:RoleClaimType"] = "roles",
    };

    /// <summary>Encodes <paramref name="secret"/> as UTF-8, zero-padding it on the right to the 32-byte minimum for HMAC-SHA256.</summary>
    /// <param name="secret">The shared secret to encode.</param>
    /// <returns>The UTF-8 bytes of <paramref name="secret"/>, padded to at least 32 bytes.</returns>
    public static byte[] SigningKeyBytes(string secret)
    {
        var raw = System.Text.Encoding.UTF8.GetBytes(secret);
        if (raw.Length >= 32)
        {
            return raw;
        }
        var padded = new byte[32];
        Array.Copy(raw, padded, raw.Length);
        return padded;
    }
}
