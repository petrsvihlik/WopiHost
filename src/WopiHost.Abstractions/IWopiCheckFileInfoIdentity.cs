namespace WopiHost.Abstractions;

/// <summary>
/// Identity slice of the WOPI <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo">CheckFileInfo</see>
/// response — the file's name, version, owner, and content fingerprint. Implemented by
/// <see cref="WopiCheckFileInfo"/>.
/// </summary>
/// <remarks>
/// Extracted as a role interface so callers (e.g. a custom permission provider, a test rig)
/// can take just this slice without depending on the full ~70-property response class.
/// </remarks>
public interface IWopiCheckFileInfoIdentity
{
    /// <summary>The file name including extension, no path.</summary>
    string BaseFileName { get; set; }

    /// <summary>Stable, unique owner identifier.</summary>
    string OwnerId { get; set; }

    /// <summary>File size in bytes.</summary>
    long Size { get; set; }

    /// <summary>Stable, unique identifier for the user accessing the file.</summary>
    string UserId { get; set; }

    /// <summary>Server-assigned version string. Must change on every content modification and never repeat.</summary>
    string Version { get; set; }

    /// <summary>File extension including leading <c>.</c>; falls back to parsing <see cref="BaseFileName"/> when null.</summary>
    string? FileExtension { get; set; }

    /// <summary>Maximum file-name length the host accepts (excluding extension). Default 250.</summary>
    int FileNameMaxLength { get; set; }

    /// <summary>UTC timestamp of the last modification, ISO-8601 round-trip format.</summary>
    string? LastModifiedTime { get; set; }

    /// <summary>Base64-encoded SHA-256 of the file contents (cache key).</summary>
    string? Sha256 { get; set; }

    /// <summary>Host-asserted equivalence id usable in lieu of <see cref="Sha256"/> when the host can guarantee identical-content files share the same value.</summary>
    string UniqueContentId { get; set; }
}
