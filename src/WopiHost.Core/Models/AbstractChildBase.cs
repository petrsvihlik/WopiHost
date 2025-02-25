namespace WopiHost.Core.Models;

/// <summary>
/// Represents a child object of an arbitrary type.
/// </summary>
public abstract class AbstractChildBase
{
	/// <summary>
	/// Name of the object.
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// URL pointing to the object.
	/// </summary>
	public required Uri Url { get; set; }
}
