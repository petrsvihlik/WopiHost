namespace WopiHost.Abstractions;

/// <summary>
/// Common properties for a Wopi resource.
/// </summary>
public interface IWopiResource
{
    /// <summary>
    /// Name of the resource
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Unique identifier of the resource.
    /// </summary>
    string Identifier { get; }
}