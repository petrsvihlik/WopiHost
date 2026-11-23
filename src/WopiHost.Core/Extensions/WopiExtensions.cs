using System.Security.Cryptography;
using WopiHost.Abstractions;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IWopiFile"/>.
/// </summary>
public static class WopiExtensions
{
    /// <summary>
    /// Gets the Base64-encoded SHA-256 checksum of the file, computing it from the file's contents
    /// when <see cref="IWopiFile.Checksum"/> is not provided.
    /// </summary>
    /// <param name="file">The file to get the checksum of.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The Base64-encoded SHA-256 checksum.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is <see langword="null"/>.</exception>
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
