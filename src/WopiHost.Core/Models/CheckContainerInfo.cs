namespace WopiHost.Core.Models;

	/// <summary>
	/// Model implmented in accordance with the new specification https://docs.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo
	/// Does not implement CheckFolderInfo members from the original specification (https://msdn.microsoft.com/en-us/library/hh642596.aspx)
	/// </summary>
	public class CheckContainerInfo
	{
		#region "Required properties"

		/// <summary>
		/// The name of the container without a path. This value will be displayed in the WOPI client UI.
		/// </summary>
		public string Name { get; set; }

		#endregion

		#region "Optional properties"

		/// <summary>
		/// A URI to a webpage for the container.
		/// </summary>
		public string HostUrl { get; set; }

		/// <summary>
		/// A Boolean value indicating whether the user is an education user or not. This should match the IsEduUser value returned in CheckFileInfo.
		/// </summary>
		public bool IsEduUser { get; set; }

		/// <summary>
		/// A Boolean value indicating whether the user is a business user or not. This should match the LicenseCheckForEditIsEnabled value returned in CheckFileInfo.
		/// </summary>
		public bool LicenseCheckForEditIsEnabled { get; set; }

		/// <summary>
		/// A URI to a webpage to allow the user to control sharing of the container. This is analogous to the FileSharingUrl in CheckFileInfo.
		/// </summary>
		public string SharingUrl { get; set; }

		/// <summary>
		/// A Boolean value that indicates the user has permission to create a new container in the container.
		/// </summary>
		public bool UserCanCreateChildContainer { get; set; }

		/// <summary>
		/// A Boolean value that indicates the user has permission to create a new file in the container.
		/// </summary>
		public bool UserCanCreateChildFile { get; set; }

		/// <summary>
		/// A Boolean value that indicates the user has permission to delete the container.
		/// </summary>
		public bool UserCanDelete { get; set; }

		/// <summary>
		/// A Boolean value that indicates the user has permission to rename the container.
		/// </summary>
		public bool UserCanRename { get; set; }

		#endregion
	}
