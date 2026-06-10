using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Tests for the default <see cref="ICheckFileInfoBuilder"/>, <see cref="ICheckContainerInfoBuilder"/>,
/// and <see cref="ICheckFolderInfoBuilder"/> implementations.
/// </summary>
public class DefaultCheckInfoBuilderTests
{
    private static Mock<IWopiPermissionProvider> CreatePermissionProvider()
    {
        var permissionProvider = new Mock<IWopiPermissionProvider>();
        permissionProvider
            .Setup(_ => _.GetFilePermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiFilePermissions.None);
        permissionProvider
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiContainer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.None);
        return permissionProvider;
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_ReturnsCorrectInfo()
    {
        var mockFile = new Mock<IWopiFile>();
        // Stub Checksum so GetEncodedSha256 takes the early-return path; otherwise it would
        // call OpenReadAsync (also unmocked), and ComputeHashAsync(null) throws.
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
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
        var httpContext = new DefaultHttpContext();

        var builder = new DefaultCheckFileInfoBuilder(CreatePermissionProvider().Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFile.Object, httpContext.ToWopiRequestInfo(), capabilities, "userInfo text");

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
        var mockFile = new Mock<IWopiFile>();
        // Stub Checksum so GetEncodedSha256 takes the early-return path; otherwise it would
        // call OpenReadAsync (also unmocked), and ComputeHashAsync(null) throws.
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(12345);

        var writableStorageProvider = new Mock<IWopiWritableStorageProvider>();
        writableStorageProvider.Setup(_ => _.FileNameMaxLength).Returns(13);
        var httpContext = new DefaultHttpContext();

        var builder = new DefaultCheckFileInfoBuilder(
            CreatePermissionProvider().Object,
            new WopiHostExtensions(),
            writableStorageProvider.Object);
        var result = await builder.BuildAsync(mockFile.Object, httpContext.ToWopiRequestInfo());

        Assert.Equal(13, result.FileNameMaxLength);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_LeavesFileUrlNull_ByDefault()
    {
        // Pins the absence of the old default FileUrl. WOPI clients fetch FileUrl WITHOUT proof
        // signing (per the proof-keys spec), so a default pointing back at this host's
        // proof-validated GetFile endpoint produced a URL the client couldn't legally use —
        // ONLYOFFICE prefers FileUrl over GetFile and its unsigned fetch 500'd on proof
        // validation ("Download failed"). Hosts with a real unsigned download channel set
        // FileUrl via IWopiHostExtensions instead.
        var mockFile = new Mock<IWopiFile>();
        // Stub Checksum so GetEncodedSha256 takes the early-return path; otherwise it would
        // call OpenReadAsync (also unmocked), and ComputeHashAsync(null) throws.
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.Identifier).Returns("WOPITEST");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        var builder = new DefaultCheckFileInfoBuilder(
            CreatePermissionProvider().Object,
            new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFile.Object, httpContext.ToWopiRequestInfo());

        Assert.Null(result.FileUrl);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_PopulatesFileUrl_WhenOnCheckFileInfoSetsIt()
    {
        var mockFile = new Mock<IWopiFile>();
        // Stub Checksum so GetEncodedSha256 takes the early-return path; otherwise it would
        // call OpenReadAsync (also unmocked), and ComputeHashAsync(null) throws.
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.Identifier).Returns("WOPITEST");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);

        var cdnUrl = new Uri("https://cdn.example.com/file");
        var extensions = new RewritingExtensions
        {
            CheckFileInfoHandler = (context, _) => { context.CheckFileInfo.FileUrl = cdnUrl; return Task.FromResult(context.CheckFileInfo); },
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        var builder = new DefaultCheckFileInfoBuilder(
            CreatePermissionProvider().Object,
            extensions);
        var result = await builder.BuildAsync(mockFile.Object, httpContext.ToWopiRequestInfo());

        Assert.Equal(cdnUrl, result.FileUrl);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_CallsOnCheckFileInfoEvent()
    {
        var mockFile = new Mock<IWopiFile>();
        // Stub Checksum so GetEncodedSha256 takes the early-return path; otherwise it would
        // call OpenReadAsync (also unmocked), and ComputeHashAsync(null) throws.
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(12345);
        var eventFired = false;
        var extensions = new RewritingExtensions
        {
            CheckFileInfoHandler = (context, _) => { eventFired = true; return Task.FromResult(context.CheckFileInfo); },
        };
        var httpContext = new DefaultHttpContext();

        var builder = new DefaultCheckFileInfoBuilder(CreatePermissionProvider().Object, extensions);
        _ = await builder.BuildAsync(mockFile.Object, httpContext.ToWopiRequestInfo());

        Assert.True(eventFired);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_WithAuthenticatedUser()
    {
        var mockFile = new Mock<IWopiFile>();
        // Stub Checksum so GetEncodedSha256 takes the early-return path; otherwise it would
        // call OpenReadAsync (also unmocked), and ComputeHashAsync(null) throws.
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(12345);
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetFilePermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiFilePermissions.None);
        var httpContext = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new(ClaimTypes.NameIdentifier, "userId"),
                    new(ClaimTypes.Name, "test")
                ], "test auth scheme")),
        };

        var builder = new DefaultCheckFileInfoBuilder(mockSecurityHandler.Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFile.Object, httpContext.ToWopiRequestInfo());

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
    public async Task GetWopiCheckFileInfo_SupportsUpdateFalse_Cascades_UserCanNotWriteRelativeTrue()
    {
        // Per https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile,
        // a host that advertises SupportsUpdate=false must also report UserCanNotWriteRelative=true.
        // The builder enforces that cascade regardless of the per-user UserCanNotWriteRelative
        // permission flag — without this, FileEndpoints.CheckFileInfo wouldn't be able to flip
        // both signals via just SupportsUpdate.
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);

        // Permissions exclude UserCanNotWriteRelative — proving the cascade is driven by
        // capabilities, not permissions.
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetFilePermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiFilePermissions.UserCanWrite);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new(ClaimTypes.NameIdentifier, "userId"),
            ], "test auth scheme")),
        };

        var builder = new DefaultCheckFileInfoBuilder(mockSecurityHandler.Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(
            mockFile.Object,
            httpContext.ToWopiRequestInfo(),
            capabilities: new WopiHostCapabilities { SupportsUpdate = false });

        Assert.False(result.SupportsUpdate);
        Assert.True(result.UserCanNotWriteRelative);
    }

    [Fact]
    public void BuildCheckFolderInfo_ReturnsAnonymousUser_WhenNotAuthenticated()
    {
        var mockFolder = new Mock<IWopiContainer>();
        mockFolder.Setup(f => f.Name).Returns("MyFolder");
        var httpContext = new DefaultHttpContext();

        var builder = new DefaultCheckFolderInfoBuilder();
        var result = builder.Build(mockFolder.Object, httpContext.User);

        Assert.Equal("MyFolder", result.FolderName);
        Assert.True(result.IsAnonymousUser);
        Assert.Null(result.UserId);
        Assert.Null(result.UserFriendlyName);
    }

    [Fact]
    public void BuildCheckFolderInfo_WithAuthenticatedUser()
    {
        var mockFolder = new Mock<IWopiContainer>();
        mockFolder.Setup(f => f.Name).Returns("MyFolder");
        var httpContext = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new(ClaimTypes.NameIdentifier, "userId42"),
                    new(ClaimTypes.Name, "Jane Doe")
                ], "test auth scheme")),
        };

        var builder = new DefaultCheckFolderInfoBuilder();
        var result = builder.Build(mockFolder.Object, httpContext.User);

        Assert.Equal("userId42", result.UserId);
        Assert.Equal("Jane Doe", result.UserFriendlyName);
        Assert.False(result.IsAnonymousUser);
    }

    // OnCheckFolderInfo callback firing is not the builder's responsibility — it lives in
    // FoldersController.CheckFolderInfo. The callback round-trip is covered by
    // FoldersControllerTests.CheckFolderInfo_CallsOnCheckFolderInfoEvent.

    [Fact]
    public async Task GetWopiCheckContainerInfo_CallsOnCheckContainerInfoEvent()
    {
        var mockFolder = new Mock<IWopiContainer>();
        mockFolder.Setup(f => f.Name).Returns("test");
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiContainer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.None);
        var eventFired = false;
        var extensions = new RewritingExtensions
        {
            CheckContainerInfoHandler = (context, _) => { eventFired = true; return Task.FromResult(context.CheckContainerInfo); },
        };
        var httpContext = new DefaultHttpContext();

        var builder = new DefaultCheckContainerInfoBuilder(mockSecurityHandler.Object, extensions);
        var result = await builder.BuildAsync(mockFolder.Object, httpContext.User);

        Assert.Equal("test", result.Name);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_WithAllPermissions_ReturnsAllTrue()
    {
        var mockFolder = new Mock<IWopiContainer>();
        mockFolder.Setup(f => f.Name).Returns("MyContainer");
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiContainer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                WopiContainerPermissions.UserCanCreateChildContainer |
                WopiContainerPermissions.UserCanCreateChildFile |
                WopiContainerPermissions.UserCanDelete |
                WopiContainerPermissions.UserCanRename);
        var httpContext = new DefaultHttpContext();

        var builder = new DefaultCheckContainerInfoBuilder(mockSecurityHandler.Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFolder.Object, httpContext.User);

        Assert.Equal("MyContainer", result.Name);
        Assert.True(result.UserCanCreateChildContainer);
        Assert.True(result.UserCanCreateChildFile);
        Assert.True(result.UserCanDelete);
        Assert.True(result.UserCanRename);
        Assert.False(result.IsEduUser);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_AnonymousUser_ReportsIsAnonymousUserTrue()
    {
        // Spec: IsAnonymousUser "should match the IsAnonymousUser value returned in
        // CheckFileInfo." All three builders (file, folder, container) must set it from auth
        // state, otherwise anonymous users get reported as authenticated.
        var mockFolder = new Mock<IWopiContainer>();
        mockFolder.Setup(f => f.Name).Returns("AnonContainer");
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiContainer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.None);
        var httpContext = new DefaultHttpContext();  // no User → anonymous

        var builder = new DefaultCheckContainerInfoBuilder(mockSecurityHandler.Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFolder.Object, httpContext.User);

        Assert.True(result.IsAnonymousUser);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_AuthenticatedUser_ReportsIsAnonymousUserFalse()
    {
        var mockFolder = new Mock<IWopiContainer>();
        mockFolder.Setup(f => f.Name).Returns("AuthContainer");
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiContainer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.None);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new(ClaimTypes.NameIdentifier, "user-1")], "test auth scheme")),
        };

        var builder = new DefaultCheckContainerInfoBuilder(mockSecurityHandler.Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFolder.Object, httpContext.User);

        Assert.False(result.IsAnonymousUser);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_WithNoPermissions_ReturnsAllFalse()
    {
        var mockFolder = new Mock<IWopiContainer>();
        mockFolder.Setup(f => f.Name).Returns("RestrictedContainer");
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiContainer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.None);
        var httpContext = new DefaultHttpContext();

        var builder = new DefaultCheckContainerInfoBuilder(mockSecurityHandler.Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFolder.Object, httpContext.User);

        Assert.Equal("RestrictedContainer", result.Name);
        Assert.False(result.UserCanCreateChildContainer);
        Assert.False(result.UserCanCreateChildFile);
        Assert.False(result.UserCanDelete);
        Assert.False(result.UserCanRename);
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_WithPartialPermissions_ReturnsCorrectFlags()
    {
        var mockFolder = new Mock<IWopiContainer>();
        mockFolder.Setup(f => f.Name).Returns("PartialContainer");
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiContainer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.UserCanCreateChildFile | WopiContainerPermissions.UserCanRename);
        var httpContext = new DefaultHttpContext();

        var builder = new DefaultCheckContainerInfoBuilder(mockSecurityHandler.Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFolder.Object, httpContext.User);

        Assert.False(result.UserCanCreateChildContainer);
        Assert.True(result.UserCanCreateChildFile);
        Assert.False(result.UserCanDelete);
        Assert.True(result.UserCanRename);
    }

    [Fact]
    public async Task GetWopiCheckFileInfo_WithAllPermissions_ReturnsAllTrue()
    {
        var mockFile = new Mock<IWopiFile>();
        // Stub Checksum so GetEncodedSha256 takes the early-return path; otherwise it would
        // call OpenReadAsync (also unmocked), and ComputeHashAsync(null) throws.
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(100);
        var mockSecurityHandler = new Mock<IWopiPermissionProvider>();
        mockSecurityHandler
            .Setup(_ => _.GetFilePermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
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
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new(ClaimTypes.NameIdentifier, "userId"),
                    new(ClaimTypes.Name, "test")
                ], "test auth scheme")),
        };

        var builder = new DefaultCheckFileInfoBuilder(mockSecurityHandler.Object, new WopiHostExtensions());
        var result = await builder.BuildAsync(mockFile.Object, httpContext.ToWopiRequestInfo());

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
        IWopiContainer? container = null;
        var user = new ClaimsPrincipal();

        var builder = new DefaultCheckContainerInfoBuilder(CreatePermissionProvider().Object, new WopiHostExtensions());
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(container!, user));
    }

    [Fact]
    public async Task GetWopiCheckContainerInfo_ThrowsOnNullUser()
    {
        var mockFolder = new Mock<IWopiContainer>();

        var builder = new DefaultCheckContainerInfoBuilder(CreatePermissionProvider().Object, new WopiHostExtensions());
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(mockFolder.Object, null!));
    }

    /// <summary>
    /// Capturing <see cref="IWopiHostExtensions"/> used by tests to observe / rewrite the
    /// <c>CheckFileInfo</c> and <c>CheckContainerInfo</c> hook calls.
    /// </summary>
    private sealed class RewritingExtensions : WopiHostExtensions
    {
        public Func<WopiCheckFileInfoContext, CancellationToken, Task<WopiCheckFileInfo>>? CheckFileInfoHandler { get; set; }
        public Func<WopiCheckContainerInfoContext, CancellationToken, Task<WopiCheckContainerInfo>>? CheckContainerInfoHandler { get; set; }

        public override Task<WopiCheckFileInfo> OnCheckFileInfoAsync(WopiCheckFileInfoContext context, CancellationToken cancellationToken = default)
            => CheckFileInfoHandler is null
                ? base.OnCheckFileInfoAsync(context, cancellationToken)
                : CheckFileInfoHandler(context, cancellationToken);

        public override Task<WopiCheckContainerInfo> OnCheckContainerInfoAsync(WopiCheckContainerInfoContext context, CancellationToken cancellationToken = default)
            => CheckContainerInfoHandler is null
                ? base.OnCheckContainerInfoAsync(context, cancellationToken)
                : CheckContainerInfoHandler(context, cancellationToken);
    }
}
