using System.Collections.Generic;

namespace WopiHost.Models
{
	public class Folder
	{
		public IEnumerable<FolderChild> Children { get; set; }
	}
}
