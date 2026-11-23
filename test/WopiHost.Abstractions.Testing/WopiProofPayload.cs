using System.Text;

namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Builds the canonical <c>X-WOPI-Proof</c> byte layout defined by MS-WOPI: a 4-byte big-endian length
/// prefix before each of the UTF-8 access token, the UTF-8 upper-cased request URL, and the
/// 8-byte big-endian <c>X-WOPI-TimeStamp</c> ticks. WOPI clients sign these bytes with their proof key;
/// hosts verify the signature against the same layout.
/// </summary>
public static class WopiProofPayload
{
    /// <summary>
    /// Assembles the canonical proof bytes for a request.
    /// </summary>
    /// <param name="accessToken">The access token sent with the request.</param>
    /// <param name="hostUrl">The request URL, already upper-cased (invariant culture), which is the form the signature covers; this method does not case-fold it.</param>
    /// <param name="timestampTicks">The ticks sent in <c>X-WOPI-TimeStamp</c>.</param>
    /// <returns>The bytes the WOPI client signs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accessToken"/> or <paramref name="hostUrl"/> is <see langword="null"/>.</exception>
    public static byte[] Build(string accessToken, string hostUrl, long timestampTicks)
    {
        ArgumentNullException.ThrowIfNull(accessToken);
        ArgumentNullException.ThrowIfNull(hostUrl);

        var tokenBytes = Encoding.UTF8.GetBytes(accessToken);
        var hostBytes = Encoding.UTF8.GetBytes(hostUrl);
        var tsBytes = BitConverter.GetBytes(timestampTicks);
        Array.Reverse(tsBytes);

        var buffer = new List<byte>(4 + tokenBytes.Length + 4 + hostBytes.Length + 4 + tsBytes.Length);
        buffer.AddRange(BigEndian(tokenBytes.Length));
        buffer.AddRange(tokenBytes);
        buffer.AddRange(BigEndian(hostBytes.Length));
        buffer.AddRange(hostBytes);
        buffer.AddRange(BigEndian(tsBytes.Length));
        buffer.AddRange(tsBytes);
        return [.. buffer];
    }

    private static byte[] BigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return bytes;
    }
}
