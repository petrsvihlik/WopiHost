using System.Security.Cryptography;
using System.Text;

namespace WopiHost.Abstractions;

/// <summary>
/// Helpers for deriving deterministic, opaque WOPI resource identifiers from a path.
/// </summary>
/// <remarks>
/// <para>
/// Storage providers shipping with WopiHost (<c>WopiHost.FileSystemProvider</c>,
/// <c>WopiHost.AzureStorageProvider</c>) use this to compute resource ids so the same logical
/// resource always produces the same id — across process restarts and across multiple host
/// instances pointing at the same backing store. Third-party providers should follow the same
/// pattern for consistency.
/// </para>
/// <para>
/// <b>Contract:</b> the caller passes a <i>canonical</i> path — a string that already encodes
/// whatever case/separator/normalization rules the underlying store uses to compare names.
/// This helper does the hash step only; it never folds case or rewrites separators.
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>WopiHost.FileSystemProvider</c> canonicalizes via <c>Path.GetFullPath(...).ToUpperInvariant()</c>
///     because Windows/macOS filesystems are case-insensitive — two casings refer to the same
///     file and must produce the same id.
///   </description></item>
///   <item><description>
///     <c>WopiHost.AzureStorageProvider</c> passes the blob path unchanged because Azure blob
///     names are case-sensitive — <c>Folder/file.txt</c> and <c>folder/file.txt</c> are
///     distinct blobs and must get distinct ids.
///   </description></item>
/// </list>
/// <para>
/// The hash is SHA-256 (not MD5) so the id-minting path is FIPS-compatible and silent under the
/// <c>CA5351</c> analyzer. Cryptographic strength is not required — these are opaque keys — but
/// a non-weak primitive avoids policy/compliance friction on hosts that disable broken algorithms.
/// </para>
/// </remarks>
public static class WopiResourceId
{
    /// <summary>
    /// Computes a deterministic, opaque WOPI resource id from an already-canonicalized path.
    /// </summary>
    /// <param name="canonicalPath">
    /// The path in the form the underlying store uses for name equality. The caller is
    /// responsible for any case-folding / separator normalization. Pass an empty string for the
    /// root container if the store uses an empty-string sentinel.
    /// </param>
    /// <returns>A lower-case hex SHA-256 digest (64 characters).</returns>
    public static string FromCanonicalPath(string canonicalPath)
    {
        ArgumentNullException.ThrowIfNull(canonicalPath);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath)))
            .ToLowerInvariant();
    }
}
