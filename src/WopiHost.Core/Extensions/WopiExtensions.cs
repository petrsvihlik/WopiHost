using System.Security.Cryptography;
using WopiHost.Abstractions;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="IWopiFile"/>.
/// </summary>
public static class WopiExtensions
{
    /// <summary>
    /// Returns base64 encoding of checksum (or calculates it from original contents if not provided)
    /// </summary>
    /// <param name="file">File object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>base64 encoded sha256 checksum</returns>
    /// <remarks>
    /// Uses the static <see cref="SHA256.HashDataAsync(Stream, CancellationToken)"/> one-shot
    /// API rather than an instance-cached <see cref="SHA256"/>. <see cref="SHA256"/> instance
    /// methods are not thread-safe, so a shared instance called from concurrent CheckFileInfo
    /// requests could corrupt the hash or throw under load.
    /// </remarks>
    public static async Task<string> GetEncodedSha256(this IWopiFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var result = file.Checksum;
        if (result is null)
        {
            using var stream = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            result = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        return Convert.ToBase64String(result.Value.Span);
    }
}
