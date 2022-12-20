namespace WopiHost.Core.Models;

/// <summary>
/// Implemented in accordance with: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer#sample-response
/// </summary>
public class BootstrapInfo
	{
		/// <summary>
		/// A string URI for the WOPI server’s 🔧 Ecosystem endpoint, with a WOPI access token appended. A GET request to this URL will invoke the CheckEcosystem operation.
		/// </summary>
		public Uri EcosystemUrl { get; set; }

		/// <summary>
		/// A string value uniquely identifying the user making the request. This value should match the UserId value provided in <see cref="CheckFileInfo"/>. 
		/// This ID is expected to be unique per user and consistent over time. See Requirements for user identity properties for more information.
		/// </summary>
		public string UserId { get; set; }

		/// <summary>
		/// A string value identifying the user making the request. This value is used to distinguish a user’s account in the event a user has multiple accounts with a given host. This value is often an email address, though it is not required to be.
		/// </summary>
		public string SignInName { get; set; }

		/// <summary>
		/// A string that is the name of the user. This value should match the <see cref="CheckFileInfo.UserFriendlyName"/> value provided in <see cref="CheckFileInfo"/>.
		/// </summary>
		public string UserFriendlyName { get; set; }
	}
