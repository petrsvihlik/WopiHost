using System.Diagnostics.CodeAnalysis;

namespace WopiHost.Web.Oidc.Models;

/// <summary>
/// Frontend-local shape for the subset of <c>Wopi:Security</c> the OIDC sample needs:
/// the HMAC signing key it shares with the WOPI backend.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a separate, project-local POCO rather than a reference to the backend's
/// <c>WopiSecurityOptions</c>. Pure frontends should not take a project dependency on
/// <c>WopiHost.Core</c>; the contract between frontend and backend is the <em>configuration
/// path</em> (<c>Wopi:Security:SigningKey</c>) — like a URL — not the typed options class.
/// Each side keeps its own typed shape pointing at the same key.
/// </para>
/// <para>
/// In production hosts, load the key from a managed secret store (Key Vault, Secrets Manager,
/// Data Protection-protected configuration). Never commit a signing key to source.
/// </para>
/// </remarks>
public class WopiSigningOptions
{
    /// <summary>
    /// Configuration section path. Must match the backend's <c>WopiSecurityOptions.SectionName</c>
    /// so both sides bind the same key.
    /// </summary>
    public const string SectionName = "Wopi:Security";

    /// <summary>
    /// HMAC signing key bytes. Bound from a base64 string by the IConfiguration binder.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays",
        Justification = "Bound from a base64 config string by the IConfiguration BinaryConverter, which only targets byte[].")]
    public byte[]? SigningKey { get; set; }
}
