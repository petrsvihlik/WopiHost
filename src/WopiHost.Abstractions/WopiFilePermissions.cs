namespace WopiHost.Abstractions;

/// <summary>
/// Specifies the WOPI file permission flags, corresponding to the
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#user-permissions-properties">user permissions properties</see>
/// of the CheckFileInfo response.
/// </summary>
[Flags]
	public enum WopiFilePermissions
	{
		/// <summary>
		/// The user has no permissions.
		/// </summary>
		None = 0,

		/// <summary>
		/// For this user, the file cannot be changed.
		/// </summary>
		ReadOnly = 1,

		/// <summary>
		/// The WOPI client should restrict what actions the user can perform on the file. The resulting behavior depends on the WOPI client.
		/// </summary>
		RestrictedWebViewOnly = 2,

		/// <summary>
		/// The user has permission to view a broadcast of this file.
		/// </summary>
		UserCanAttend = 4,

		/// <summary>
		/// The user does not have sufficient permission to create new files on the WOPI server, so calls to PutRelativeFile fail for this user on the current file.
		/// </summary>
		UserCanNotWriteRelative = 8,

		/// <summary>
		/// The user has permission to broadcast this file to a set of users who have permission to broadcast or view a broadcast of the current file.
		/// </summary>
		UserCanPresent = 16,

		/// <summary>
		/// The user has permission to rename the current file.
		/// </summary>
		UserCanRename = 32,

		/// <summary>
		/// The user has permission to alter the file, so the WOPI client can call PutFile on behalf of the user.
		/// </summary>
		UserCanWrite = 64,

		/// <summary>
		/// The WOPI client must not allow the user to edit the file. This does not mean that the user lacks rights to edit the file; <see cref="UserCanWrite"/> expresses those.
		/// </summary>
		WebEditingDisabled = 128
	}
