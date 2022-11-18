namespace WopiHost.Core.Models;

	/// <summary>
	/// Represents a child file of a container.
	/// </summary>
	public class ChildFile : AbstractChildBase
	{
		/// <summary>
		/// Version of the file.
		/// </summary>
		public string Version { get; set; }

		/// <summary>
		/// Size of the file.
		/// </summary>
		public long Size { get; set; }

		/// <summary>
		/// Timestamp of the file's last modification.
		/// </summary>
		public string LastModifiedTime { get; set; }
	}
