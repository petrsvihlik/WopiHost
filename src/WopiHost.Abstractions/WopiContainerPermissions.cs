namespace WopiHost.Abstractions;

/// <summary>
/// Specifies the WOPI container permission flags reported in the
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo">CheckContainerInfo</see>
/// response.
/// </summary>
[Flags]
	public enum WopiContainerPermissions
	{
		/// <summary>
		/// The user has no permissions.
		/// </summary>
		None = 0,

		/// <summary>
		/// The user has permission to create a new child container.
		/// </summary>
		UserCanCreateChildContainer = 1,

		/// <summary>
		/// The user has permission to create a new child file.
		/// </summary>
		UserCanCreateChildFile = 2,

		/// <summary>
		/// The user has permission to delete the container.
		/// </summary>
		UserCanDelete = 4,

		/// <summary>
		/// The user has permission to rename the container.
		/// </summary>
		UserCanRename = 8,
	}
