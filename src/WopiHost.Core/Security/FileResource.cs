namespace WopiHost.Core.Security;

	/// <summary>
	/// Represents a resource for a resource-based authroization.
	/// </summary>
	public class FileResource
{
		/// <summary>
		/// Identifier of a resource.
		/// </summary>
		public string FileId { get; }

		/// <summary>
		/// Creates an object representing a resource for a resource-based authroization.
		/// </summary>
		/// <param name="fileId">Identifier of a resource.</param>
		public FileResource(string fileId)
	    {
		    FileId = fileId;
	    }
}
