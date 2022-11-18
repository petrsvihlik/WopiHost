namespace WopiHost.Core.Models;

/// <summary>
/// Represents a child object of an arbitrary type.
/// </summary>
public abstract class AbstractChildBase
	{
		/// <summary>
		/// Name of the object.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// URL pointing to the object.
		/// </summary>
		public Uri Url { get; set; }
	}
