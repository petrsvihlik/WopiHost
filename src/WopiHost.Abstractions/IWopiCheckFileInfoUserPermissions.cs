namespace WopiHost.Abstractions;

/// <summary>
/// User-permission slice of the WOPI <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#user-permissions-properties">CheckFileInfo</see>
/// response. Mirrors the spec's "User permissions properties" section. Implemented by
/// <see cref="WopiCheckFileInfo"/>.
/// </summary>
/// <remarks>
/// <para>
/// These flags must be consistent with what the access token actually authorizes. The default
/// permission resolution flow is: <see cref="IWopiPermissionProvider"/> computes a
/// <see cref="WopiFilePermissions"/> bitmask, which gets baked into the access token at
/// mint time and re-projected onto these flags at <c>CheckFileInfo</c> time.
/// </para>
/// <para>
/// Hosts that want to expose a "permissions only" surface to a custom policy can take this
/// interface instead of the full <see cref="WopiCheckFileInfo"/>.
/// </para>
/// </remarks>
public interface IWopiCheckFileInfoUserPermissions
{
    /// <summary>The user can edit the file (PutFile is allowed).</summary>
    bool UserCanWrite { get; set; }

    /// <summary>The user can rename the file (RenameFile is allowed).</summary>
    bool UserCanRename { get; set; }

    /// <summary>The user can attend a meeting/coauth session for this file.</summary>
    bool UserCanAttend { get; set; }

    /// <summary>The user can present in a coauth session for this file.</summary>
    bool UserCanPresent { get; set; }

    /// <summary>The user is NOT allowed to PutRelativeFile (Save As / convert).</summary>
    bool UserCanNotWriteRelative { get; set; }

    /// <summary>For this user, the file cannot be modified.</summary>
    bool ReadOnly { get; set; }

    /// <summary>The WOPI client should restrict actions on the file beyond view-only.</summary>
    bool RestrictedWebViewOnly { get; set; }

    /// <summary>Web editing is disabled for this file regardless of <see cref="UserCanWrite"/>.</summary>
    bool WebEditingDisabled { get; set; }
}
