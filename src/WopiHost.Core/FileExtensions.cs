using System.Globalization;
using System.Security.Cryptography;
using WopiHost.Abstractions;

namespace WopiHost.Core;

/// <summary>
/// Extension methods for <see cref="IWopiFile"/>.
/// </summary>
public static class FileExtensions
{
    private static readonly SHA256 Sha = SHA256.Create();

    /// <summary>
    /// Returns base64 encoding of checksum (or calculates it from original contents if not provided)
    /// </summary>
    /// <param name="file">File object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>base64 encoded sha256 checksum</returns>
    public static async Task<string> GetEncodedSha256(this IWopiFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var result = file.Checksum;
        if (result is null)
        {
            using var stream = await file.GetReadStream(cancellationToken);
            result = await Sha.ComputeHashAsync(stream, cancellationToken);
        }
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiCheckFileInfo"/> based on the provided <see cref="IWopiFile"/> and <see cref="WopiHostCapabilities"/>.
    /// </summary>
    /// <param name="file"><see cref="IWopiFile"/> to return info</param>
    /// <param name="capabilities"><see cref="WopiHostCapabilities"/> to include in result</param>
    public static WopiCheckFileInfo GetWopiCheckFileInfo(this IWopiFile file, WopiHostCapabilities? capabilities = null)
    {
        // #181 make sure the BaseFileName always has an extensions
        var baseFileName = file.Name.EndsWith(file.Extension, StringComparison.OrdinalIgnoreCase)
            ? file.Name
            : file.Name + "." + file.Extension.TrimStart('.');

        var result = new WopiCheckFileInfo
        {
            UserId = string.Empty,
            OwnerId = file.Owner.ToSafeIdentity(),
            Version = file.Version ?? file.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture),
            FileExtension = "." + file.Extension.TrimStart('.'),
            BaseFileName = baseFileName,
            LastModifiedTime = file.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
            Size = file.Exists ? file.Length : 0,
        };
        if (capabilities is not null)
        {
            // Set host capabilities
            result.SupportsCoauth = capabilities.SupportsCoauth;
            result.SupportsFolders = capabilities.SupportsFolders;
            result.SupportsLocks = capabilities.SupportsLocks;
            result.SupportsGetLock = capabilities.SupportsGetLock;
            result.SupportsExtendedLockLength = capabilities.SupportsExtendedLockLength;
            result.SupportsEcosystem = capabilities.SupportsEcosystem;
            result.SupportsGetFileWopiSrc = capabilities.SupportsGetFileWopiSrc;
            result.SupportedShareUrlTypes = capabilities.SupportedShareUrlTypes;
            result.SupportsScenarioLinks = capabilities.SupportsScenarioLinks;
            result.SupportsSecureStore = capabilities.SupportsSecureStore;
            result.SupportsUpdate = capabilities.SupportsUpdate;
            result.SupportsCobalt = capabilities.SupportsCobalt;
            result.SupportsRename = capabilities.SupportsRename;
            result.SupportsDeleteFile = capabilities.SupportsDeleteFile;
            result.SupportsUserInfo = capabilities.SupportsUserInfo;
            result.SupportsFileCreation = capabilities.SupportsFileCreation;
        }
        return result;
    }
}
