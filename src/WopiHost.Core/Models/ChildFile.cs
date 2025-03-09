namespace WopiHost.Core.Models;

/// <summary>
/// Represents a child file of a container.
/// </summary>
/// <param name="Name">Name of the object.</param>
/// <param name="Url">URL pointing to the object.</param>
public record ChildFile(string Name, string Url) : AbstractChildBase(Name, Url)
{
	/// <summary>
	/// Version of the file.
	/// </summary>
	public string? Version { get; set; }

	/// <summary>
	/// Size of the file.
	/// </summary>
	public long Size { get; set; }

	/// <summary>
	/// Timestamp of the file's last modification.
	/// </summary>
	public string? LastModifiedTime { get; set; }
}
