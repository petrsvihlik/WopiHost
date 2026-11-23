using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Represents a string that is transmitted UTF-7 encoded, as the WOPI spec requires for some headers.
/// </summary>
/// <remarks><see cref="ToString()"/> and the implicit conversion to <see cref="string"/> return the decoded value.</remarks>
public class UtfString : IParsable<UtfString>
{
    /// <summary>
    /// Gets the decoded value.
    /// </summary>
    protected string? DecodedValue { get; init; }
    /// <summary>
    /// Gets the UTF-7 encoded value.
    /// </summary>
    protected string? EncodedValue { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UtfString"/> class with no value.
    /// </summary>
    public UtfString()
    {
    }

    /// <summary>
    /// Creates a <see cref="UtfString"/> from a decoded string.
    /// </summary>
    /// <param name="decodedValue">The decoded string.</param>
    /// <returns>A <see cref="UtfString"/> holding <paramref name="decodedValue"/> and its UTF-7 encoding.</returns>
    public static UtfString FromDecoded(string? decodedValue)
    {
        return new UtfString { EncodedValue = EncodeString(decodedValue), DecodedValue = decodedValue };
    }

    /// <summary>
    /// Creates a <see cref="UtfString"/> from a UTF-7 encoded string.
    /// </summary>
    /// <param name="encodedValue">The UTF-7 encoded string.</param>
    /// <returns>A <see cref="UtfString"/> holding <paramref name="encodedValue"/> and its decoded value.</returns>
    public static UtfString FromEncoded(string? encodedValue)
    {
        return new UtfString { EncodedValue = encodedValue, DecodedValue = DecodeString(encodedValue) };
    }

    /// <summary>
    /// Parses a UTF-7 encoded string.
    /// </summary>
    /// <param name="s">The UTF-7 encoded string.</param>
    /// <param name="provider">Not used.</param>
    /// <returns>The parsed <see cref="UtfString"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="s"/> is <see langword="null"/>.</exception>
    public static UtfString Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new ArgumentException("Could not parse supplied value.", nameof(s));
        }
        return result;
    }

    /// <inheritdoc/>
    public static bool TryParse(string? s, IFormatProvider? provider, out UtfString outValue)
    {
        if (s is null)
        {
            outValue = new UtfString() { EncodedValue = s };
            return false;
        }
        
        outValue = new UtfString { EncodedValue = s, DecodedValue = DecodeString(s) };
        return true;
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        return DecodedValue;
    }

    /// <summary>
    /// Returns the encoded or decoded string value.
    /// </summary>
    /// <param name="asEncoded"><see langword="true"/> to return the UTF-7 encoded value; <see langword="false"/> to return the decoded value.</param>
    /// <returns>The requested value, or <see langword="null"/> if none is set.</returns>
    public string? ToString(bool asEncoded)
    {
        return asEncoded ? EncodedValue : DecodedValue;
    }

    /// <summary>
    /// Converts a <see cref="UtfString"/> to its decoded string value.
    /// </summary>
    /// <param name="utfString">The value to convert.</param>
    /// <returns>The decoded value, or <see langword="null"/> if <paramref name="utfString"/> is <see langword="null"/>.</returns>
    [return: NotNullIfNotNull(nameof(utfString))]
    public static implicit operator string?(UtfString? utfString)
    {
        return utfString?.ToString();
    }

    private static string? DecodeString(string? encodedValue)
    {
        if (encodedValue is null)
        {
            return encodedValue;
        }
        byte[] utf7Bytes = Encoding.Default.GetBytes(encodedValue);

        // SYSLIB0001 is suppressed here because the WOPI specification mandates UTF-7 for the
        // X-WOPI-SuggestedTarget and X-WOPI-RelativeTarget headers — see
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile#request-headers
        // and the MS-WOPI protocol spec section 2.2.5.1.5. The framework deprecation is unrelated
        // to the protocol requirement; a different encoding would break interop with Office
        // Online / Microsoft 365 for the Web.
#pragma warning disable SYSLIB0001 // Type or member is obsolete — required by WOPI spec
        Encoding utf7 = Encoding.UTF7;
#pragma warning restore SYSLIB0001
        return utf7.GetString(utf7Bytes);
    }

    /// <summary>
    /// Encodes a string as UTF-7.
    /// </summary>
    /// <param name="decodedValue">The string to encode.</param>
    /// <returns>The UTF-7 encoded value.</returns>
    private static string? EncodeString(string? decodedValue)
    {
        if (decodedValue is null)
        {
            return decodedValue;
        }
        // SYSLIB0001 suppressed — see DecodeString for rationale (WOPI spec mandates UTF-7).
#pragma warning disable SYSLIB0001 // Type or member is obsolete — required by WOPI spec
        Encoding utf7 = Encoding.UTF7;
#pragma warning restore SYSLIB0001

        byte[] bytes = utf7.GetBytes(decodedValue);

        return Encoding.Default.GetString(bytes);
    }
}