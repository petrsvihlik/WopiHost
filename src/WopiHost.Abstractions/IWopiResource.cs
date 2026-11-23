namespace WopiHost.Abstractions;

/// <summary>
/// Represents the properties common to WOPI files and containers.
/// </summary>
public interface IWopiResource
{
    /// <summary>
    /// Gets the name of the resource.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the unique, opaque identifier of the resource.
    /// </summary>
    string Identifier { get; }
}