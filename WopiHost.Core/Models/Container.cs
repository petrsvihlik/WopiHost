namespace WopiHost.Core.Models;

/// <summary>
/// An object representing a WOPI Container.
/// </summary>
public class Container
	{
		/// <summary>
		/// A collection containing child containers.
		/// </summary>
		public IEnumerable<ChildContainer> ChildContainers { get; set; }
		
		/// <summary>
		/// A collection containing child files.
		/// </summary>
		public IEnumerable<ChildFile> ChildFiles { get; set; }
	}
