using System;
using System.Security.Claims;
using System.Security.Cryptography;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core
{
    public static class FileExtensions
    {
        private static readonly SHA256 SHA = SHA256.Create();

        public static CheckFileInfo GetCheckFileInfo(this IWopiFile file, ClaimsPrincipal principal, HostCapabilities capabilities)
        {
            CheckFileInfo CheckFileInfo = new CheckFileInfo();
            if (principal != null)
            {
                CheckFileInfo.UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                CheckFileInfo.UserFriendlyName = principal.FindFirst(ClaimTypes.Name)?.Value;

                WopiUserPermissions permissions = (WopiUserPermissions)Enum.Parse(typeof(WopiUserPermissions), principal.FindFirst(WopiClaimTypes.UserPermissions).Value);

                CheckFileInfo.ReadOnly = permissions.HasFlag(WopiUserPermissions.ReadOnly);
                CheckFileInfo.RestrictedWebViewOnly = permissions.HasFlag(WopiUserPermissions.RestrictedWebViewOnly);
                CheckFileInfo.UserCanAttend = permissions.HasFlag(WopiUserPermissions.UserCanAttend);
                CheckFileInfo.UserCanNotWriteRelative = permissions.HasFlag(WopiUserPermissions.UserCanNotWriteRelative);
                CheckFileInfo.UserCanPresent = permissions.HasFlag(WopiUserPermissions.UserCanPresent);
                CheckFileInfo.UserCanRename = permissions.HasFlag(WopiUserPermissions.UserCanRename);
                CheckFileInfo.UserCanWrite = permissions.HasFlag(WopiUserPermissions.UserCanWrite);
                CheckFileInfo.WebEditingDisabled = permissions.HasFlag(WopiUserPermissions.WebEditingDisabled);
            }
            else
            {
                CheckFileInfo.IsAnonymousUser = true;
            }

            CheckFileInfo.OwnerId = file.Owner;

            // Set host capabilities
            CheckFileInfo.SupportsCoauth = capabilities.SupportsCoauth;
            CheckFileInfo.SupportsFolders = capabilities.SupportsFolders;
            CheckFileInfo.SupportsLocks = capabilities.SupportsLocks;
            CheckFileInfo.SupportsGetLock = capabilities.SupportsGetLock;
            CheckFileInfo.SupportsExtendedLockLength = capabilities.SupportsExtendedLockLength;
            CheckFileInfo.SupportsEcosystem = capabilities.SupportsEcosystem;
            CheckFileInfo.SupportsGetFileWopiSrc = capabilities.SupportsGetFileWopiSrc;
            CheckFileInfo.SupportedShareUrlTypes = capabilities.SupportedShareUrlTypes;
            CheckFileInfo.SupportsScenarioLinks = capabilities.SupportsScenarioLinks;
            CheckFileInfo.SupportsSecureStore = capabilities.SupportsSecureStore;
            CheckFileInfo.SupportsUpdate = capabilities.SupportsUpdate;
            CheckFileInfo.SupportsCobalt = capabilities.SupportsCobalt;
            CheckFileInfo.SupportsRename = capabilities.SupportsRename;
            CheckFileInfo.SupportsDeleteFile = capabilities.SupportsDeleteFile;
            CheckFileInfo.SupportsUserInfo = capabilities.SupportsUserInfo;
            CheckFileInfo.SupportsFileCreation = capabilities.SupportsFileCreation;

            using (var stream = file.GetReadStream())
            {
                byte[] checksum = SHA.ComputeHash(stream);
                CheckFileInfo.SHA256 = Convert.ToBase64String(checksum);
            }
            CheckFileInfo.BaseFileName = file.Name;
            CheckFileInfo.FileExtension = "." + file.Extension.TrimStart('.');
            CheckFileInfo.Version = file.LastWriteTimeUtc.ToString("s");
            CheckFileInfo.LastModifiedTime = file.LastWriteTimeUtc.ToString("o");
            CheckFileInfo.Size = file.Exists ? file.Length : 0;
            return CheckFileInfo;
        }
    }
}
