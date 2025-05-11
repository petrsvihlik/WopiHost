namespace WopiHost.Discovery.Models;

/// <summary>
/// Represents the proof keys provided in the WOPI discovery XML.
/// </summary>
public class WopiProofKeys
{
    /// <summary>
    /// The current proof key value (for .NET applications).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// The old proof key value (for .NET applications).
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// The current proof key modulus.
    /// </summary>
    public string? Modulus { get; set; }

    /// <summary>
    /// The current proof key exponent.
    /// </summary>
    public string? Exponent { get; set; }

    /// <summary>
    /// The old proof key modulus.
    /// </summary>
    public string? OldModulus { get; set; }

    /// <summary>
    /// The old proof key exponent.
    /// </summary>
    public string? OldExponent { get; set; }
}