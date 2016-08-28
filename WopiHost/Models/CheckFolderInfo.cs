namespace WopiHost.Models
{
	/// <summary>
	/// Model according to https://msdn.microsoft.com/en-us/library/hh642596.aspx
	/// </summary>
	public class CheckFolderInfo
	{
		//TODO: add the rest of the props & comments from https://msdn.microsoft.com/en-us/library/hh642596.aspx
		//TODO: http://wopi.readthedocs.io/projects/wopirest/en/latest/containers/CheckContainerInfo.html?highlight=checkcontainerinfo
		public string Name { get; set; }

		public string OwnerId { get; set; }
	}
}
