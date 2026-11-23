namespace WopiHost.Core.Models;

/// <summary>
/// Represents a child file of a container.
/// </summary>
/// <param name="Name">The name of the file, including its extension.</param>
/// <param name="Url">The WopiSrc URL of the file.</param>
public record ChildFile(string Name, Uri Url) : AbstractChildBase(Name, Url)
{
	/// <summary>
	/// Gets the version of the file.
	/// </summary>
	public string? Version { get; init; }

	/// <summary>
	/// Gets the size of the file, in bytes.
	/// </summary>
	public long Size { get; init; }

	/// <summary>
	/// Gets the time of the file's last modification, as an ISO 8601 string.
	/// </summary>
	public string? LastModifiedTime { get; init; }

    /// <summary>
    /// Gets the URL that views the file in the host's web interface.
    /// </summary>
    public Uri? HostViewUrl { get; init; }

    /// <summary>
    /// Gets the URL that edits the file in the host's web interface.
    /// </summary>
    public Uri? HostEditUrl { get; init; }
}
