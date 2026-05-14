using System.Collections.ObjectModel;
using System.Security.Claims;
using Moq;
using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

/// <summary>
/// Tests for the default <see cref="WopiHostExtensions"/> pass-through implementation. Hosts
/// extend this class and override the hooks they need; the un-overridden defaults must echo
/// the input context or complete as a no-op, never mutate state.
/// </summary>
public class WopiHostExtensionsTests
{
    private readonly WopiHostExtensions _sut = new();

    [Fact]
    public async Task OnCheckFileInfoAsync_ReturnsContextCheckFileInfo()
    {
        var info = new WopiCheckFileInfo { BaseFileName = "doc.docx", OwnerId = "u1", UserId = "u1", Version = "v1" };
        var ctx = new WopiCheckFileInfoContext(new ClaimsPrincipal(), Mock.Of<IWopiFile>(), info);

        var result = await _sut.OnCheckFileInfoAsync(ctx);

        Assert.Same(info, result);
    }

    [Fact]
    public async Task OnCheckContainerInfoAsync_ReturnsContextCheckContainerInfo()
    {
        var info = new WopiCheckContainerInfo { Name = "folder" };
        var ctx = new WopiCheckContainerInfoContext(new ClaimsPrincipal(), Mock.Of<IWopiContainer>(), info);

        var result = await _sut.OnCheckContainerInfoAsync(ctx);

        Assert.Same(info, result);
    }

    [Fact]
    public async Task OnCheckFolderInfoAsync_ReturnsContextCheckFolderInfo()
    {
        var info = new WopiCheckFolderInfo { FolderName = "folder" };
        var ctx = new WopiCheckFolderInfoContext(new ClaimsPrincipal(), Mock.Of<IWopiContainer>(), info);

        var result = await _sut.OnCheckFolderInfoAsync(ctx);

        Assert.Same(info, result);
    }

    [Fact]
    public async Task OnCheckEcosystemAsync_ReturnsContextCheckEcosystem()
    {
        var info = new WopiCheckEcosystem();
        var ctx = new WopiCheckEcosystemContext(new ClaimsPrincipal(), info);

        var result = await _sut.OnCheckEcosystemAsync(ctx);

        Assert.Same(info, result);
    }

    [Fact]
    public async Task OnPutFileAsync_IsNoOp()
    {
        var ctx = new WopiPutFileContext(new ClaimsPrincipal(), Mock.Of<IWopiFile>(), new ReadOnlyCollection<string>([]));

        // The default pass-through impl completes without inspecting the context. The visible
        // behavior is "it doesn't throw and returns a completed task".
        await _sut.OnPutFileAsync(ctx);
    }

    [Fact]
    public async Task OnPutRelativeFileAsync_IsNoOp()
    {
        var ctx = new WopiPutRelativeFileContext(
            new ClaimsPrincipal(),
            Mock.Of<IWopiFile>(),
            Mock.Of<IWopiFile>(),
            IsFileConversion: true,
            DeclaredSize: 1024);

        await _sut.OnPutRelativeFileAsync(ctx);
    }

    [Fact]
    public async Task OnCheckFileInfoAsync_NullContext_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.OnCheckFileInfoAsync(null!));

    [Fact]
    public async Task OnCheckContainerInfoAsync_NullContext_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.OnCheckContainerInfoAsync(null!));

    [Fact]
    public async Task OnCheckFolderInfoAsync_NullContext_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.OnCheckFolderInfoAsync(null!));

    [Fact]
    public async Task OnCheckEcosystemAsync_NullContext_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.OnCheckEcosystemAsync(null!));
}
