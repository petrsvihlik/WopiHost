namespace WopiHost.Abstractions;

/// <summary>
/// WOPI claims  implemented in accordance with: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#user-permissions-properties
/// </summary>
[Flags]
	public enum WopiUserPermissions
	{
		/// <summary>
		/// Default value - user has no permissions.
		/// </summary>
		None = 0,

		/// <summary>
		/// A Boolean value that indicates that, for this user, the file cannot be changed.
		/// </summary>
		ReadOnly = 1,

		/// <summary>
		/// A Boolean value that indicates that the WOPI client should restrict what actions the user can perform on the file. The behavior of this property is dependent on the WOPI client.
		/// </summary>
		RestrictedWebViewOnly = 2,

		/// <summary>
		/// A Boolean value that indicates that the user has permission to view a broadcast of this file.
		/// </summary>
		UserCanAttend = 4,

		/// <summary>
		/// A Boolean value that indicates the user does not have sufficient permission to create new files on the WOPI server. Setting this to true tells the WOPI client that calls to PutRelativeFile will fail for this user on the current file.
		/// </summary>
		UserCanNotWriteRelative = 8,

		/// <summary>
		/// A Boolean value that indicates that the user has permission to broadcast this file to a set of users who have permission to broadcast or view a broadcast of the current file.
		/// </summary>
		UserCanPresent = 16,

		/// <summary>
		/// A Boolean value that indicates the user has permission to rename the current file.
		/// </summary>
		UserCanRename = 32,

		/// <summary>
		/// A Boolean value that indicates that the user has permission to alter the file. Setting this to true tells the WOPI client that it can call PutFile on behalf of the user.
		/// </summary>
		UserCanWrite = 64,

		/// <summary>
		/// A Boolean value that indicates that the WOPI client must not allow the user to edit the file. This does not mean that the user doesn’t have rights to edit the file. Hosts should use the UserCanWrite property for that purpose.
		/// </summary>
		WebEditingDisabled = 128
	}
