namespace WopiHost.Models
{
	/// <summary>
	/// Implemented in accordance with: https://wopi.readthedocs.io/projects/wopirest/en/latest/bootstrapper/GetRootContainer.html#sample-response
	/// </summary>
	public class BootstrapInfo
	{
		public string EcosystemUrl { get; set; }

		public string UserId { get; set; }

		public string SignInName { get; set; }

		public string UserFriendlyName { get; set; }
	}
}
