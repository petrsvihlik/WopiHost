using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Validator.Infrastructure;

public static class WopiEvents
{
    /// <summary>
    /// Custom handling of CheckFileInfo results for WOPI-Validator
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static Task<WopiCheckFileInfo> OnGetWopiCheckFileInfo(WopiCheckFileInfoContext context)
    {
        var wopiCheckFileInfo = context.CheckFileInfo;
        wopiCheckFileInfo.AllowAdditionalMicrosoftServices = true;
        wopiCheckFileInfo.AllowErrorReportPrompt = true;

        // ##183 required for WOPI-Validator
        if (wopiCheckFileInfo.BaseFileName.EndsWith(".wopitest", StringComparison.OrdinalIgnoreCase))
        {
            wopiCheckFileInfo.CloseUrl = new("https://example.com/close");
            wopiCheckFileInfo.DownloadUrl = new("https://example.com/download");
            wopiCheckFileInfo.FileSharingUrl = new("https://example.com/share");
            wopiCheckFileInfo.FileUrl = new("https://example.com/file");
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
        }
        return Task.FromResult(wopiCheckFileInfo);
    }
}