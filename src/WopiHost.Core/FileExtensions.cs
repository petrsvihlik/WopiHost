using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core;

/// <summary>
/// Extension methods for <see cref="IWopiFile"/>.
/// </summary>
public static class FileExtensions
{
    private static readonly SHA256 Sha = SHA256.Create();

    /// <summary>
    /// Returns a CheckFileInfo model according to https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo
    /// </summary>
    /// <param name="file">File properties of which should be returned.</param>
    /// <param name="principal">A user object which the CheckFileInfo should be correlated with.</param>
    /// <param name="capabilities">WOPI host capabilities</param>
    /// <returns>CheckFileInfo model</returns>
    public static CheckFileInfo GetCheckFileInfo(this IWopiFile file, ClaimsPrincipal principal, HostCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(file);

        ArgumentNullException.ThrowIfNull(capabilities);

        var checkFileInfo = new CheckFileInfo();
        if (principal is not null)
        {
            checkFileInfo.UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.ToSafeIdentity();
            checkFileInfo.UserFriendlyName = principal.FindFirst(ClaimTypes.Name)?.Value;

            var permissions = (WopiUserPermissions)Enum.Parse(typeof(WopiUserPermissions), principal.FindFirst(WopiClaimTypes.USER_PERMISSIONS).Value);

            checkFileInfo.ReadOnly = permissions.HasFlag(WopiUserPermissions.ReadOnly);
            checkFileInfo.RestrictedWebViewOnly = permissions.HasFlag(WopiUserPermissions.RestrictedWebViewOnly);
            checkFileInfo.UserCanAttend = permissions.HasFlag(WopiUserPermissions.UserCanAttend);
            checkFileInfo.UserCanNotWriteRelative = permissions.HasFlag(WopiUserPermissions.UserCanNotWriteRelative);
            checkFileInfo.UserCanPresent = permissions.HasFlag(WopiUserPermissions.UserCanPresent);
            checkFileInfo.UserCanRename = permissions.HasFlag(WopiUserPermissions.UserCanRename);
            checkFileInfo.UserCanWrite = permissions.HasFlag(WopiUserPermissions.UserCanWrite);
            checkFileInfo.WebEditingDisabled = permissions.HasFlag(WopiUserPermissions.WebEditingDisabled);
        }
        else
        {
            checkFileInfo.IsAnonymousUser = true;
        }

        checkFileInfo.OwnerId = file.Owner.ToSafeIdentity();

        // Set host capabilities
        checkFileInfo.SupportsCoauth = capabilities.SupportsCoauth;
        checkFileInfo.SupportsFolders = capabilities.SupportsFolders;
        checkFileInfo.SupportsLocks = capabilities.SupportsLocks;
        checkFileInfo.SupportsGetLock = capabilities.SupportsGetLock;
        checkFileInfo.SupportsExtendedLockLength = capabilities.SupportsExtendedLockLength;
        checkFileInfo.SupportsEcosystem = capabilities.SupportsEcosystem;
        checkFileInfo.SupportsGetFileWopiSrc = capabilities.SupportsGetFileWopiSrc;
        checkFileInfo.SupportedShareUrlTypes = capabilities.SupportedShareUrlTypes;
        checkFileInfo.SupportsScenarioLinks = capabilities.SupportsScenarioLinks;
        checkFileInfo.SupportsSecureStore = capabilities.SupportsSecureStore;
        checkFileInfo.SupportsUpdate = capabilities.SupportsUpdate;
        checkFileInfo.SupportsCobalt = capabilities.SupportsCobalt;
        checkFileInfo.SupportsRename = capabilities.SupportsRename;
        checkFileInfo.SupportsDeleteFile = capabilities.SupportsDeleteFile;
        checkFileInfo.SupportsUserInfo = capabilities.SupportsUserInfo;
        checkFileInfo.SupportsFileCreation = capabilities.SupportsFileCreation;

        using (var stream = file.GetReadStream())
        {
            var checksum = Sha.ComputeHash(stream);
            checkFileInfo.Sha256 = Convert.ToBase64String(checksum);
        }
        checkFileInfo.BaseFileName = file.Name;
        checkFileInfo.FileExtension = "." + file.Extension.TrimStart('.');
        checkFileInfo.Version = file.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture);
        checkFileInfo.LastModifiedTime = file.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture);
        checkFileInfo.Size = file.Exists ? file.Length : 0;
        return checkFileInfo;
    }
}
