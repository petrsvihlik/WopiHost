namespace WopiHost.Core.Models;

/// <summary>
/// Represents a child file of a container.
/// </summary>
/// <param name="Name">Name of the object.</param>
/// <param name="Url">URL pointing to the object.</param>
public record ChildFile(string Name, Uri Url) : AbstractChildBase(Name, Url)
{
	/// <summary>
	/// Version of the file.
	/// </summary>
	public string? Version { get; init; }

	/// <summary>
	/// Size of the file.
	/// </summary>
	public long Size { get; init; }

	/// <summary>
	/// Timestamp of the file's last modification.
	/// </summary>
	public string? LastModifiedTime { get; init; }

    /// <summary>
    /// URL to view the file in the host's web interface.
    /// </summary>
    public Uri? HostViewUrl { get; init; }

    /// <summary>
    /// URL to edit the file in the host's web interface.
    /// </summary>
    public Uri? HostEditUrl { get; init; }
}
