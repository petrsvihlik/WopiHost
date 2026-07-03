using System.Text;

namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Builds the canonical X-WOPI-Proof byte layout defined by MS-WOPI: a 4-byte big-endian length
/// prefix before each of the UTF-8 access token, the UTF-8 upper-cased request URL, and the
/// 8-byte big-endian X-WOPI-TimeStamp ticks. WOPI clients sign these bytes with their proof key;
/// hosts verify the signature against the same layout.
/// </summary>
public static class WopiProofPayload
{
    /// <summary>
    /// Assembles the canonical proof bytes for <paramref name="accessToken"/>,
    /// <paramref name="hostUrl"/> (already case-folded to upper-invariant, the form the
    /// signature covers), and the <paramref name="timestampTicks"/> sent in X-WOPI-TimeStamp.
    /// </summary>
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
