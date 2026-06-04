using WopiHost.Abstractions;

namespace WopiHost.Validator.Infrastructure;

/// <summary>
/// Validator-specific <see cref="IWopiHostExtensions"/>. The Microsoft WOPI-Validator suite
/// checks for several optional CheckFileInfo URLs (<c>HostEditUrl</c>, <c>BreadcrumbBrandUrl</c>,
/// etc.) on the canonical <c>.wopitest</c> probe file, which are populated here.
/// </summary>
public class WopiValidatorExtensions : WopiHostExtensions
{
    /// <inheritdoc />
    public override Task<WopiCheckFileInfo> OnCheckFileInfoAsync(WopiCheckFileInfoContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var wopiCheckFileInfo = context.CheckFileInfo;
        wopiCheckFileInfo.AllowAdditionalMicrosoftServices = true;
        wopiCheckFileInfo.AllowErrorReportPrompt = true;

        // Required for the WOPI-Validator probe file.
        if (wopiCheckFileInfo.BaseFileName.EndsWith(".wopitest", StringComparison.OrdinalIgnoreCase))
        {
            wopiCheckFileInfo.CloseUrl = new("https://example.com/close");
            wopiCheckFileInfo.DownloadUrl = new("https://example.com/download");
            wopiCheckFileInfo.FileSharingUrl = new("https://example.com/share");
            // FileUrl is populated by the framework with a self-pointing GetFile URL.
            wopiCheckFileInfo.FileVersionUrl = new("https://example.com/version");
            wopiCheckFileInfo.HostEditUrl = new("https://example.com/edit");
            wopiCheckFileInfo.HostEmbeddedViewUrl = new("https://example.com/embedded");
            wopiCheckFileInfo.HostEmbeddedEditUrl = new("https://example.com/embeddededit");
            wopiCheckFileInfo.HostRestUrl = new("https://example.com/rest");
            wopiCheckFileInfo.HostViewUrl = new("https://example.com/view");
            wopiCheckFileInfo.SignInUrl = new("https://example.com/signin");
            wopiCheckFileInfo.SignoutUrl = new("https://example.com/signout");

            wopiCheckFileInfo.ClientUrl = new("https://example.com/client");
            wopiCheckFileInfo.FileEmbedCommandUrl = new("https://example.com/embed");

            // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-other#breadcrumb-properties
            wopiCheckFileInfo.BreadcrumbBrandName = "WopiHost";
            wopiCheckFileInfo.BreadcrumbBrandUrl = new("https://example.com");
            wopiCheckFileInfo.BreadcrumbDocName = "test";
            wopiCheckFileInfo.BreadcrumbFolderName = "root";
            wopiCheckFileInfo.BreadcrumbFolderUrl = new("https://example.com/folder");

            // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-other#unused-and-future-properties
            wopiCheckFileInfo.HostNotes = string.Empty;
            wopiCheckFileInfo.IrmPolicyDescription = string.Empty;
            wopiCheckFileInfo.IrmPolicyTitle = string.Empty;
            wopiCheckFileInfo.PresenceProvider = string.Empty;
            wopiCheckFileInfo.PresenceUserId = string.Empty;
            wopiCheckFileInfo.TenantId = string.Empty;
            wopiCheckFileInfo.TimeZone = string.Empty;
        }
        return Task.FromResult(wopiCheckFileInfo);
    }
}
