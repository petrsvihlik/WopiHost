namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Shared in-memory configuration values used by both <see cref="OidcWebAppFactory"/>
/// (real OIDC handshake) and <see cref="TestAuthOidcWebAppFactory"/> (TestAuthHandler stand-in).
/// Centralised so the WOPI / OIDC config keys aren't duplicated in two factory files.
/// </summary>
internal static class OidcSampleTestConfig
{
    /// <summary>Client id the sample registers with the IdP. Mock-oauth2-server accepts any id.</summary>
    public const string TestClientId = "wopihost-oidc-sample";

    /// <summary>Client secret. Mock-oauth2-server accepts any value here.</summary>
    public const string TestClientSecret = "test-client-secret";

    /// <summary>
    /// Builds the in-memory config dictionary applied to the OIDC sample web host.
    /// </summary>
    public static Dictionary<string, string?> Build(string oidcAuthority, string wopiSigningSecret, string wopiBackendUrl) => new()
    {
        ["Wopi:HostUrl"] = wopiBackendUrl,
        ["Wopi:ClientUrl"] = "https://office.example.test",
        ["Wopi:Discovery:NetZone"] = "ExternalHttps",
        ["Wopi:Discovery:RefreshInterval"] = "12:00:00",
        ["Wopi:StorageProvider:RootPath"] = TestPaths.WopiDocsRoot,
        ["Wopi:Security:SigningKey"] = Convert.ToBase64String(SigningKeyBytes(wopiSigningSecret)),
        ["Oidc:Authority"] = oidcAuthority,
        ["Oidc:ClientId"] = TestClientId,
        ["Oidc:ClientSecret"] = TestClientSecret,
        ["Oidc:RequireHttpsMetadata"] = "false",
        ["Oidc:UsePkce"] = "true",
        ["Oidc:RoleClaimType"] = "roles",
    };

    /// <summary>Right-pads a UTF-8 secret to the 32-byte minimum for HMAC-SHA256.</summary>
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
