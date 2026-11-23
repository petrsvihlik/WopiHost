namespace WopiHost.Core.Models;

/// <summary>
/// Represents the contents of a WOPI container.
/// </summary>
public class Container
{
	/// <summary>
	/// Gets or sets the child containers.
	/// </summary>
	public IEnumerable<ChildContainer> ChildContainers { get; set; } = [];
		
	/// <summary>
	/// Gets or sets the child files.
	/// </summary>
	public IEnumerable<ChildFile> ChildFiles { get; set; } = [];
}
