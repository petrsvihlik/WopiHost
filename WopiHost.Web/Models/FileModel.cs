namespace WopiHost.Web.Models
{
	public class FileModel
	{
		//TODO: considering abstracting to wopi.abstractions along with FolderChild
		public string Name { get; set; }

		public string Url { get; set; }

		public string Version { get; set; }
	}
}