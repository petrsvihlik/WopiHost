using System.Collections.Generic;

namespace WopiHost.Web.Models
{
	public class Folder
	{
		public IEnumerable<File> Children { get; set; }
	}
}
