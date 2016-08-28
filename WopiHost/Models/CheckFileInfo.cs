namespace WopiHost.Models
{
	/// <summary>
	/// Model according to https://msdn.microsoft.com/en-us/library/hh622920.aspx
	/// </summary>
	public class CheckFileInfo : ChildFile
	{
		//TODO: Enrich with comments from the https://msdn.microsoft.com/en-us/library/hh622920.aspx
		public bool AllowExternalMarketplace { get; set; }

		public string BaseFileName { get { return Name; } set { Name = value; } }

		public string BreadcrumbBrandName { get; set; }

		public string BreadcrumbBrandUrl { get; set; }

		public string BreadcrumbDocName { get; set; }

		public string BreadcrumbDocUrl { get; set; }

		public string BreadcrumbFolderName { get; set; }

		public string BreadcrumbFolderUrl { get; set; }

		public string ClientUrl { get; set; }

		public bool CloseButtonClosesWindow { get; set; }

		public string CloseUrl { get; set; }

		public bool DisableBrowserCachingOfUserContent { get; set; }

		public bool DisablePrint { get; set; }

		public bool DisableTranslation { get; set; }

		public string DownloadUrl { get; set; }

		public string FileSharingUrl { get; set; }

		public string FileUrl { get; set; }

		public string HostAuthenticationId { get; set; }

		public string HostEditUrl { get; set; }

		public string HostEmbeddedEditUrl { get; set; }

		public string HostEmbeddedViewUrl { get; set; }

		public string HostName { get; set; }

		public string HostNotes { get; set; }

		public string HostRestUrl { get; set; }

		public string HostViewUrl { get; set; }

		public string IrmPolicyDescription { get; set; }

		public string IrmPolicyTitle { get; set; }

		public string OwnerId { get; set; }

		public string PresenceProvider { get; set; }

		public string PresenceUserId { get; set; }

		public string PrivacyUrl { get; set; }

		public bool ProtectInClient { get; set; }

		public bool ReadOnly { get; set; }

		public bool RestrictedWebViewOnly { get; set; }

		public string SHA256 { get; set; }

		public string SignoutUrl { get; set; }

		public bool SupportsCoauth { get; set; }

		public bool SupportsCobalt { get; set; }

		public bool SupportsFolders { get { return SupportsContainers; } set { SupportsContainers = value; } }

		public bool SupportsContainers { get; set; }

		public bool SupportsLocks { get; set; }

		public bool SupportsScenarioLinks { get; set; }

		public bool SupportsSecureStore { get; set; }

		public bool SupportsUpdate { get; set; }

		public string TenantId { get; set; }

		public string TermsOfUseUrl { get; set; }

		public string TimeZone { get; set; }

		public bool UserCanAttend { get; set; }

		public bool UserCanNotWriteRelative { get; set; }

		public bool UserCanPresent { get; set; }

		public bool UserCanWrite { get; set; }

		public string UserFriendlyName { get; set; }

		public string UserId { get; set; }

		public bool WebEditingDisabled { get; set; }
	}
}
