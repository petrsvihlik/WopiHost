namespace WopiHost.Core;

internal static class Extensions
{
    /// <summary>
    /// Copies the stream to a byte array.
    /// </summary>
    /// <param name="input">Stream to read from</param>
    /// <returns>Byte array copy of a stream</returns>
    public static async Task<byte[]> ReadBytesAsync(this Stream input)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Tries to parse integer from string. Returns null if parsing fails.
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <returns>Integer parsed from <paramref name="s"/></returns>
    public static int? ToNullableInt(this string s)
    {
        if (int.TryParse(s, out var i)) return i;
        return null;
    }

    /// <summary>
    /// Converts <see cref="DateTime"/> to UNIX timestamp.
    /// </summary>
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        DateTimeOffset dto = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return dto.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Replaces forbidden characters in identity properties with an underscore.
    /// Accordingly to: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#requirements-for-user-identity-properties
    /// </summary>
    /// <param name="identity">Identity property value</param>
    /// <returns>String safe to use as an identity property</returns>
    public static string ToSafeIdentity(this string identity)
    {
        const string forbiddenChars = "<>\"#{}^[]`\\/";
        return forbiddenChars.Aggregate(identity, (current, forbiddenChar) => current.Replace(forbiddenChar, '_'));
    }
}