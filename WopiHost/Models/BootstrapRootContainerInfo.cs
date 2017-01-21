namespace WopiHost.Models
{
	/// <summary>
	/// Implemented in accordance with: https://wopirest.readthedocs.io/en/latest/bootstrapper/GetRootContainer.html#sample-response
	/// </summary>
	public class BootstrapRootContainerInfo
	{
		public RootContainerInfo RootContainerInfo { get; set; }

		public BootstrapInfo Bootstrap { get; set; }

		public AccessTokenInfo AccessTokenInfo { get; set; }
	}
}
