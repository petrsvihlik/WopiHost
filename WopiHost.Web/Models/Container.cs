using System.Collections.Generic;

namespace WopiHost.Web.Models
{
	public class Container
	{
		public IEnumerable<Child> ChildContainers { get; set; }

		public IEnumerable<Child> ChildFiles { get; set; }
	}
}
