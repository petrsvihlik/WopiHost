namespace WopiHost.Abstractions;

/// <summary>
/// WOPI container permission flags implemented in accordance with: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo
/// </summary>
[Flags]
	public enum WopiContainerPermissions
	{
		/// <summary>
		/// Default value - user has no permissions.
		/// </summary>
		None = 0,

		/// <summary>
		/// A Boolean value that indicates that the user has permission to create a new child container.
		/// </summary>
		UserCanCreateChildContainer = 1,

		/// <summary>
		/// A Boolean value that indicates that the user has permission to create a new child file.
		/// </summary>
		UserCanCreateChildFile = 2,

		/// <summary>
		/// A Boolean value that indicates that the user has permission to delete the container.
		/// </summary>
		UserCanDelete = 4,

		/// <summary>
		/// A Boolean value that indicates that the user has permission to rename the container.
		/// </summary>
		UserCanRename = 8,
	}
