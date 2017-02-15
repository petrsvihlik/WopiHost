using System.Collections.Generic;

namespace WopiHost.Core.Models
{
	public class Container
	{
		public IEnumerable<ChildContainer> ChildContainers { get; set; }
		
		public IEnumerable<ChildFile> ChildFiles { get; set; }
	}
}
