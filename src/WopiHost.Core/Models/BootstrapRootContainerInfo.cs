namespace WopiHost.Core.Models;

	/// <summary>
	/// Implemented in accordance with: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer#sample-response
	/// </summary>
	public class BootstrapRootContainerInfo
	{
		/// <summary>
		/// Object describing the root container.
		/// </summary>
		public RootContainerInfo RootContainerInfo { get; set; }

		/// <summary>
		/// Object with properties necessary for calling the /bootstrap operation.
		/// </summary>
		public BootstrapInfo Bootstrap { get; set; }

		/// <summary>
		/// A WOPI access token.
		/// </summary>
		public AccessTokenInfo AccessTokenInfo { get; set; }
	}
