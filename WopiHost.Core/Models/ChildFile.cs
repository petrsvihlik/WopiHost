namespace WopiHost.Core.Models
{
	public class ChildFile : AbstractChildBase
	{

		public string Version { get; set; }

		public long Size { get; set; }

		public string LastModifiedTime { get; set; }
	}
}
