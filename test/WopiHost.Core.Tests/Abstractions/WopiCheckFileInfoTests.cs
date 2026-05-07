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
    public void ImplementsAllRoleInterfaces()
    {
        var sut = MinimallyValid();
        // Each role interface is a documented slice of the response. WopiCheckFileInfo
        // is the canonical implementation; verify a freshly-built instance is assignable
        // to every slice contract.
        Assert.IsAssignableFrom<IWopiCheckFileInfoIdentity>(sut);
        Assert.IsAssignableFrom<IWopiCheckFileInfoUserPermissions>(sut);
        Assert.IsAssignableFrom<IWopiCheckFileInfoUserMetadata>(sut);
        Assert.IsAssignableFrom<IWopiCheckFileInfoBreadcrumb>(sut);
        Assert.IsAssignableFrom<IWopiHostCapabilities>(sut);
    }

    [Fact]
    public void RolePermissionSlice_SharesBackingStoreWithFlatProperties()
    {
        // The point of the role interfaces is to give callers (e.g. a custom permission
        // policy) a narrower contract — but the slice must NOT introduce a separate state
        // pocket. Setting through the interface and reading off the concrete type (or
        // vice versa) must observe the same value.
        WopiCheckFileInfo sut = MinimallyValid();
        IWopiCheckFileInfoUserPermissions perms = sut;

        perms.UserCanWrite = true;
        perms.ReadOnly = true;

        Assert.True(sut.UserCanWrite);
        Assert.True(sut.ReadOnly);

        sut.UserCanRename = true;
        Assert.True(perms.UserCanRename);
    }

    [Fact]
    public void PermissionPolicy_CanTakeJustTheSliceContract()
    {
        // Demonstrates the documented value of the decomposition: a function that only
        // needs to set permissions can take IWopiCheckFileInfoUserPermissions and never
        // see (or be tempted to mutate) the rest of the response.
        static void ApplyReadOnlyPolicy(IWopiCheckFileInfoUserPermissions perms)
        {
            perms.UserCanWrite = false;
            perms.UserCanRename = false;
            perms.ReadOnly = true;
            perms.RestrictedWebViewOnly = true;
        }

        var sut = MinimallyValid();
        sut.UserCanWrite = true;
        sut.UserCanRename = true;

        ApplyReadOnlyPolicy(sut);

        Assert.False(sut.UserCanWrite);
        Assert.False(sut.UserCanRename);
        Assert.True(sut.ReadOnly);
        Assert.True(sut.RestrictedWebViewOnly);
    }

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
