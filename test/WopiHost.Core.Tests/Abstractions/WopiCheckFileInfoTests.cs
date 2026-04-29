using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

public class WopiCheckFileInfoTests
{
    private static WopiCheckFileInfo MinimallyValid() => new()
    {
        BaseFileName = "doc.docx",
        OwnerId = "owner",
        UserId = "user",
        Version = "1",
    };

    [Fact]
    public void OptionalProperties_RoundTrip()
    {
        // Round-trip every property the existing GetWopiCheckFileInfo
        // extension does not populate, so coverage reflects assignment + read.
        var sut = MinimallyValid();
        var docUrl = new Uri("https://host/doc");
        var editAndReply = new Uri("https://host/edit-and-reply");
        var privacy = new Uri("https://host/privacy");

#pragma warning disable CS0618 // exercising deprecated members deliberately
        sut.BreadcrumbDocUrl = docUrl;
        sut.HostName = "host-name";
        sut.PrivacyUrl = privacy;
#pragma warning restore CS0618

        sut.CobaltCapabilities = ["DownloadStreaming"];
        sut.CloseButtonClosesWindow = true;
        sut.DisableBrowserCachingOfUserContent = true;
        sut.AllowErrorReportPrompt = true;
        sut.EditAndReplyUrl = editAndReply;
        sut.ProtectInClient = true;
        sut.FileEmbedCommandPostMessage = true;

#pragma warning disable CS0618
        Assert.Equal(docUrl, sut.BreadcrumbDocUrl);
        Assert.Equal("host-name", sut.HostName);
        Assert.Equal(privacy, sut.PrivacyUrl);
#pragma warning restore CS0618
        Assert.Equal(["DownloadStreaming"], sut.CobaltCapabilities);
        Assert.True(sut.CloseButtonClosesWindow);
        Assert.True(sut.DisableBrowserCachingOfUserContent);
        Assert.True(sut.AllowErrorReportPrompt);
        Assert.Equal(editAndReply, sut.EditAndReplyUrl);
        Assert.True(sut.ProtectInClient);
        Assert.True(sut.FileEmbedCommandPostMessage);
    }
}
