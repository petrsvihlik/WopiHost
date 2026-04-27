using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Models;

namespace WopiHost.Core.Tests.Extensions;

public class WopiExtensionsTests
{
    [Fact]
    public async Task GetEncodedSha256_ReturnsBase64Checksum_WhenChecksumIsProvided()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        var checksum = new byte[] { 1, 2, 3, 4, 5 };
        mockFile.Setup(f => f.Checksum).Returns(checksum);

        // Act
        var result = await mockFile.Object.GetEncodedSha256();

        // Assert
        Assert.Equal(Convert.ToBase64String(checksum), result);
    }

    [Fact]
    public async Task GetEncodedSha256_CalculatesChecksum_WhenChecksumIsNotProvided()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Checksum).Returns((byte[]?)null);
        var stream = new MemoryStream([1, 2, 3, 4, 5]);
        mockFile.Setup(f => f.GetReadStream(It.IsAny<CancellationToken>())).ReturnsAsync(stream);

        // Act
        var result = await mockFile.Object.GetEncodedSha256();

        // Assert
        var stream2 = new MemoryStream([1, 2, 3, 4, 5]);
        var expectedChecksum = await SHA256.Create().ComputeHashAsync(stream2);
        Assert.Equal(Convert.ToBase64String(expectedChecksum), result);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_ReturnsCorrectInfo()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(12345);

        var capabilities = new WopiHostCapabilities
        {
            SupportsCoauth = true,
            SupportsFolders = true,
            SupportsLocks = true,
            SupportsGetLock = true,
            SupportsExtendedLockLength = true,
            SupportsEcosystem = true,
            SupportsGetFileWopiSrc = true,
            SupportedShareUrlTypes = ["ReadOnly", "ReadWrite"],
            SupportsScenarioLinks = true,
            SupportsSecureStore = true,
            SupportsUpdate = true,
            SupportsCobalt = true,
            SupportsRename = true,
            SupportsDeleteFile = true,
            SupportsUserInfo = true,
            SupportsFileCreation = true
        };
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(),
        };

        // Act
        var result = await mockFile.Object.GetWopiCheckFileInfo(httpContext, capabilities, "userInfo text");

        // Assert
        Assert.Equal("owner", result.OwnerId);
        Assert.Equal("test.txt", result.BaseFileName);
        Assert.Equal(".txt", result.FileExtension);
        Assert.Equal(0, result.FileNameMaxLength);
        Assert.Equal(12345, result.Size);
        Assert.Equal("userInfo text", result.UserInfo);
        Assert.True(result.SupportsCoauth);
        Assert.True(result.SupportsFolders);
        Assert.True(result.SupportsLocks);
        Assert.True(result.SupportsGetLock);
        Assert.True(result.SupportsExtendedLockLength);
        Assert.True(result.SupportsEcosystem);
        Assert.True(result.SupportsGetFileWopiSrc);
        Assert.Equal(["ReadOnly", "ReadWrite"], result.SupportedShareUrlTypes);
        Assert.True(result.SupportsScenarioLinks);
        Assert.True(result.SupportsSecureStore);
        Assert.True(result.SupportsUpdate);
        Assert.True(result.SupportsCobalt);
        Assert.True(result.SupportsRename);
        Assert.True(result.SupportsDeleteFile);
        Assert.True(result.SupportsUserInfo);
        Assert.True(result.SupportsFileCreation);
        Assert.True(result.IsAnonymousUser);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_WithWritableStorageProvider_ReturnsCorrectInfo()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(12345);

        var writableStorageProvider = new Mock<IWopiWritableStorageProvider>();
        writableStorageProvider.Setup(_ => _.FileNameMaxLength).Returns(13);
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(writableStorageProvider.Object),
        };

        // Act
        var result = await mockFile.Object.GetWopiCheckFileInfo(httpContext);

        // Assert
        Assert.Equal(13, result.FileNameMaxLength);
    }

    private sealed class StubLinkGenerator(string returnValue) : LinkGenerator
    {
        public override string? GetPathByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary? ambientValues = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions? options = null) => returnValue;
        public override string? GetPathByAddress<TAddress>(TAddress address, RouteValueDictionary values, PathString pathBase = default, FragmentString fragment = default, LinkOptions? options = null) => returnValue;
        public override string? GetUriByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary? ambientValues = null, string? scheme = null, HostString? host = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions? options = null) => returnValue;
        public override string? GetUriByAddress<TAddress>(TAddress address, RouteValueDictionary values, string? scheme, HostString host, PathString pathBase = default, FragmentString fragment = default, LinkOptions? options = null) => returnValue;
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_PopulatesFileUrl_WhenLinkGeneratorRegistered()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.Identifier).Returns("WOPITEST");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);

        const string expected = "https://localhost/wopi/files/WOPITEST/contents?access_token=tok";
        var linkGenerator = new StubLinkGenerator(expected);

        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope<LinkGenerator>(linkGenerator),
        };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        // Act
        var result = await mockFile.Object.GetWopiCheckFileInfo(httpContext);

        // Assert
        Assert.NotNull(result.FileUrl);
        Assert.Equal(expected, result.FileUrl.ToString());
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_OnCheckFileInfo_CanOverrideFileUrl()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.Identifier).Returns("WOPITEST");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);

        var linkGenerator = new StubLinkGenerator("https://localhost/wopi/files/WOPITEST/contents?access_token=tok");

        var cdnUrl = new Uri("https://cdn.example.com/file");
        var wopiHostOptions = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnCheckFileInfo = context => { context.CheckFileInfo.FileUrl = cdnUrl; return Task.FromResult(context.CheckFileInfo); },
        });

        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope<LinkGenerator, IOptions<WopiHostOptions>>(linkGenerator, wopiHostOptions),
        };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        // Act
        var result = await mockFile.Object.GetWopiCheckFileInfo(httpContext);

        // Assert: host's override wins over framework default.
        Assert.Equal(cdnUrl, result.FileUrl);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_FileUrl_StaysNull_WhenLinkGeneratorMissing()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.Identifier).Returns("WOPITEST");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);

        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(),
        };

        // Act
        var result = await mockFile.Object.GetWopiCheckFileInfo(httpContext);

        // Assert
        Assert.Null(result.FileUrl);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_CallsOnCheckFileInfoEvent()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(12345);
        var eventFired = false;
        var wopiHostOptions = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnCheckFileInfo = context => { eventFired = true; return Task.FromResult(context.CheckFileInfo); }
        });
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(wopiHostOptions),
        };

        // Act
        var result = await mockFile.Object.GetWopiCheckFileInfo(httpContext);

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_WithAuthenticatedUser()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(12345);
        var mockSecurityHandler = new Mock<IWopiSecurityHandler>();
        mockSecurityHandler
            .Setup(_ => _.GetFilePermissions(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiFilePermissions.None);
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(mockSecurityHandler.Object),
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new(ClaimTypes.NameIdentifier, "userId"),
                    new(ClaimTypes.Name, "test")
                ], "test auth scheme")),
        };

        // Act
        var result = await mockFile.Object.GetWopiCheckFileInfo(httpContext);

        // Assert
        Assert.Equal("userId", result.UserId);
        Assert.Equal("userId", result.HostAuthenticationId);
        Assert.Equal("test", result.UserFriendlyName);
        Assert.Null(result.UserPrincipalName);
        Assert.False(result.ReadOnly);
        Assert.False(result.RestrictedWebViewOnly);
        Assert.False(result.UserCanAttend);
        Assert.False(result.UserCanNotWriteRelative);
        Assert.False(result.UserCanPresent);
        Assert.False(result.UserCanRename);
        Assert.False(result.UserCanWrite);
        Assert.False(result.WebEditingDisabled);
        Assert.False(result.IsAnonymousUser);
    }

    [Fact]
    public async Task GetWopiCheckFolderInfo_ReturnsAnonymousUser_WhenNotAuthenticated()
    {
        // Arrange
        var mockFolder = new Mock<IWopiFolder>();
        mockFolder.Setup(f => f.Name).Returns("MyFolder");
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(),
        };

        // Act
        var result = await mockFolder.Object.GetWopiCheckFolderInfo(httpContext);

        // Assert
        Assert.Equal("MyFolder", result.FolderName);
        Assert.True(result.IsAnonymousUser);
        Assert.Null(result.UserId);
        Assert.Null(result.UserFriendlyName);
    }

    [Fact]
    public async Task GetWopiCheckFolderInfo_WithAuthenticatedUser()
    {
        // Arrange
        var mockFolder = new Mock<IWopiFolder>();
        mockFolder.Setup(f => f.Name).Returns("MyFolder");
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(),
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new(ClaimTypes.NameIdentifier, "userId42"),
                    new(ClaimTypes.Name, "Jane Doe")
                ], "test auth scheme")),
        };

        // Act
        var result = await mockFolder.Object.GetWopiCheckFolderInfo(httpContext);

        // Assert
        Assert.Equal("userId42", result.UserId);
        Assert.Equal("Jane Doe", result.UserFriendlyName);
        Assert.False(result.IsAnonymousUser);
    }

    [Fact]
    public async Task GetWopiCheckFolderInfo_CallsOnCheckFolderInfoEvent()
    {
        // Arrange
        var mockFolder = new Mock<IWopiFolder>();
        mockFolder.Setup(f => f.Name).Returns("MyFolder");
        var eventFired = false;
        var hostViewUrl = new Uri("https://host/view");
        var hostEditUrl = new Uri("https://host/edit");
        var closeUrl = new Uri("https://host/close");
        var fileSharingUrl = new Uri("https://host/share");
        var brandUrl = new Uri("https://brand.example.com");
        var folderUrl = new Uri("https://host/parent");
        var wopiHostOptions = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnCheckFolderInfo = context =>
            {
                eventFired = true;
                // Verify context record is properly constructed
                Assert.Equal("MyFolder", context.CheckFolderInfo.FolderName);
                Assert.NotNull(context.Folder);
                // Set all optional properties to verify they round-trip
                context.CheckFolderInfo.OwnerId = "owner1";
                context.CheckFolderInfo.UserCanWrite = true;
                context.CheckFolderInfo.HostViewUrl = hostViewUrl;
                context.CheckFolderInfo.HostEditUrl = hostEditUrl;
                context.CheckFolderInfo.CloseUrl = closeUrl;
                context.CheckFolderInfo.FileSharingUrl = fileSharingUrl;
                context.CheckFolderInfo.BreadcrumbBrandName = "Contoso";
                context.CheckFolderInfo.BreadcrumbBrandUrl = brandUrl;
                context.CheckFolderInfo.BreadcrumbFolderName = "ParentFolder";
                context.CheckFolderInfo.BreadcrumbFolderUrl = folderUrl;
                context.CheckFolderInfo.DisablePrint = true;
                context.CheckFolderInfo.CloseButtonClosesWindow = true;
                return Task.FromResult(context.CheckFolderInfo);
            }
        });
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(wopiHostOptions),
        };

        // Act
        var result = await mockFolder.Object.GetWopiCheckFolderInfo(httpContext);

        // Assert
        Assert.True(eventFired);
        Assert.Equal("owner1", result.OwnerId);
        Assert.True(result.UserCanWrite);
        Assert.Equal(hostViewUrl, result.HostViewUrl);
        Assert.Equal(hostEditUrl, result.HostEditUrl);
        Assert.Equal(closeUrl, result.CloseUrl);
        Assert.Equal(fileSharingUrl, result.FileSharingUrl);
        Assert.Equal("Contoso", result.BreadcrumbBrandName);
        Assert.Equal(brandUrl, result.BreadcrumbBrandUrl);
        Assert.Equal("ParentFolder", result.BreadcrumbFolderName);
        Assert.Equal(folderUrl, result.BreadcrumbFolderUrl);
        Assert.True(result.DisablePrint);
        Assert.True(result.CloseButtonClosesWindow);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_CallsOnCheckContainerInfoEvent()
    {
        // Arrange
        var mockFolder = new Mock<IWopiFolder>();
        mockFolder.Setup(f => f.Name).Returns("test");
        var mockSecurityHandler = new Mock<IWopiSecurityHandler>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissions(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.None);
        var eventFired = false;
        var wopiHostOptions = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnCheckContainerInfo = context => { eventFired = true; return Task.FromResult(context.CheckContainerInfo); }
        });
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>, IWopiSecurityHandler>(wopiHostOptions, mockSecurityHandler.Object),
        };

        // Act
        var result = await mockFolder.Object.GetWopiCheckContainerInfo(httpContext);

        // Assert
        Assert.Equal("test", result.Name);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_WithAllPermissions_ReturnsAllTrue()
    {
        // Arrange
        var mockFolder = new Mock<IWopiFolder>();
        mockFolder.Setup(f => f.Name).Returns("MyContainer");
        var mockSecurityHandler = new Mock<IWopiSecurityHandler>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissions(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                WopiContainerPermissions.UserCanCreateChildContainer |
                WopiContainerPermissions.UserCanCreateChildFile |
                WopiContainerPermissions.UserCanDelete |
                WopiContainerPermissions.UserCanRename);
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(mockSecurityHandler.Object),
        };

        // Act
        var result = await mockFolder.Object.GetWopiCheckContainerInfo(httpContext);

        // Assert
        Assert.Equal("MyContainer", result.Name);
        Assert.True(result.UserCanCreateChildContainer);
        Assert.True(result.UserCanCreateChildFile);
        Assert.True(result.UserCanDelete);
        Assert.True(result.UserCanRename);
        Assert.False(result.IsEduUser);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_WithNoPermissions_ReturnsAllFalse()
    {
        // Arrange
        var mockFolder = new Mock<IWopiFolder>();
        mockFolder.Setup(f => f.Name).Returns("RestrictedContainer");
        var mockSecurityHandler = new Mock<IWopiSecurityHandler>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissions(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.None);
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(mockSecurityHandler.Object),
        };

        // Act
        var result = await mockFolder.Object.GetWopiCheckContainerInfo(httpContext);

        // Assert
        Assert.Equal("RestrictedContainer", result.Name);
        Assert.False(result.UserCanCreateChildContainer);
        Assert.False(result.UserCanCreateChildFile);
        Assert.False(result.UserCanDelete);
        Assert.False(result.UserCanRename);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_WithPartialPermissions_ReturnsCorrectFlags()
    {
        // Arrange
        var mockFolder = new Mock<IWopiFolder>();
        mockFolder.Setup(f => f.Name).Returns("PartialContainer");
        var mockSecurityHandler = new Mock<IWopiSecurityHandler>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissions(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.UserCanCreateChildFile | WopiContainerPermissions.UserCanRename);
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(mockSecurityHandler.Object),
        };

        // Act
        var result = await mockFolder.Object.GetWopiCheckContainerInfo(httpContext);

        // Assert
        Assert.False(result.UserCanCreateChildContainer);
        Assert.True(result.UserCanCreateChildFile);
        Assert.False(result.UserCanDelete);
        Assert.True(result.UserCanRename);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_WithAllPermissions_ReturnsAllTrue()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(100);
        var mockSecurityHandler = new Mock<IWopiSecurityHandler>();
        mockSecurityHandler
            .Setup(_ => _.GetFilePermissions(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                WopiFilePermissions.ReadOnly |
                WopiFilePermissions.RestrictedWebViewOnly |
                WopiFilePermissions.UserCanAttend |
                WopiFilePermissions.UserCanNotWriteRelative |
                WopiFilePermissions.UserCanPresent |
                WopiFilePermissions.UserCanRename |
                WopiFilePermissions.UserCanWrite |
                WopiFilePermissions.WebEditingDisabled);
        var httpContext = new DefaultHttpContext()
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(mockSecurityHandler.Object),
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new(ClaimTypes.NameIdentifier, "userId"),
                    new(ClaimTypes.Name, "test")
                ], "test auth scheme")),
        };

        // Act
        var result = await mockFile.Object.GetWopiCheckFileInfo(httpContext);

        // Assert
        Assert.True(result.ReadOnly);
        Assert.True(result.RestrictedWebViewOnly);
        Assert.True(result.UserCanAttend);
        Assert.True(result.UserCanNotWriteRelative);
        Assert.True(result.UserCanPresent);
        Assert.True(result.UserCanRename);
        Assert.True(result.UserCanWrite);
        Assert.True(result.WebEditingDisabled);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_ThrowsOnNullContainer()
    {
        // Arrange
        IWopiFolder? container = null;
        var httpContext = new DefaultHttpContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => container!.GetWopiCheckContainerInfo(httpContext));
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_ThrowsOnNullHttpContext()
    {
        // Arrange
        var mockFolder = new Mock<IWopiFolder>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => mockFolder.Object.GetWopiCheckContainerInfo(null!));
    }
}
