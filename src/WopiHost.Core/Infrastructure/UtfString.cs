using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Model for parsing/creating UTF-7 encoded strings.
/// </summary>
/// <remarks>the .ToString and implicit string conversion always return the Decoded value</remarks>
public class UtfString : IParsable<UtfString>
{
    /// <summary>
    /// The decoded value.
    /// </summary>
    protected string? DecodedValue { get; init; }
    /// <summary>
    /// The encoded value.
    /// </summary>
    protected string? EncodedValue { get; init; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public UtfString()
    {
    }

    /// <summary>
    /// Create an instance of <see cref="UtfString"/> given a decoded string
    /// </summary>
    /// <param name="decodedValue"></param>
    public static UtfString FromDecoded(string? decodedValue)
    {
        return new UtfString { EncodedValue = EncodeString(decodedValue), DecodedValue = decodedValue };
    }

    /// <summary>
    /// Create an instance of <see cref="UtfString"/> given an UTF-7 encoded string
    /// </summary>
    /// <param name="encodedValue"></param>
    public static UtfString FromEncoded(string? encodedValue)
    {
        return new UtfString { EncodedValue = encodedValue, DecodedValue = DecodeString(encodedValue) };
    }

    /// <summary>
    /// Parses an encoded value
    /// </summary>
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
    /// Returns the string representation of the value.
    /// </summary>
    /// <param name="asEncoded">whether to return UTF-7 encoded or the original string.</param>
    /// <returns></returns>
    public string? ToString(bool asEncoded)
    {
        return asEncoded ? EncodedValue : DecodedValue;
    }

    /// <summary>
    /// Implicit conversion from UtfString to string.
    /// </summary>
    /// <param name="utfString"></param>
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
    /// Conversion from string to UtfString.
    /// </summary>
    /// <param name="decodedValue"></param>
    /// <returns>UTF-7 encoded value</returns>
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