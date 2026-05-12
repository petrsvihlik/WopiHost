using Microsoft.Extensions.DependencyInjection;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class WopiLockAwareWritableStorageProviderTests
{
    private readonly Mock<IWopiWritableStorageProvider> _innerMock = new();
    private readonly Mock<IWopiLockProvider> _lockProviderMock = new();

    private WopiLockAwareWritableStorageProvider CreateDecorator()
        => new(_innerMock.Object, _lockProviderMock.Object);

    [Fact]
    public async Task DeleteWopiResource_NoLock_DelegatesToInner()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("file-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WopiLockInfo?)null);
        _innerMock.Setup(x => x.DeleteWopiResource<IWopiFile>("file-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateDecorator().DeleteWopiResource<IWopiFile>("file-1");

        Assert.True(result);
        _innerMock.Verify(x => x.DeleteWopiResource<IWopiFile>("file-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteWopiResource_Locked_ThrowsAndDoesNotDelegate()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("file-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { FileId = "file-1", LockId = "active-lock" });

        var ex = await Assert.ThrowsAsync<WopiResourceLockedException>(
            () => CreateDecorator().DeleteWopiResource<IWopiFile>("file-1"));
        Assert.Equal("file-1", ex.ResourceIdentifier);
        Assert.Equal("active-lock", ex.LockId);
        _innerMock.Verify(x => x.DeleteWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RenameWopiResource_NoLock_DelegatesToInner()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("file-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WopiLockInfo?)null);
        _innerMock.Setup(x => x.RenameWopiResource<IWopiFile>("file-2", "renamed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateDecorator().RenameWopiResource<IWopiFile>("file-2", "renamed");

        Assert.True(result);
        _innerMock.Verify(x => x.RenameWopiResource<IWopiFile>("file-2", "renamed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenameWopiResource_Locked_ThrowsAndDoesNotDelegate()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("file-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { FileId = "file-2", LockId = "lock-x" });

        await Assert.ThrowsAsync<WopiResourceLockedException>(
            () => CreateDecorator().RenameWopiResource<IWopiFile>("file-2", "renamed"));
        _innerMock.Verify(x => x.RenameWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateWopiChildResource_BypassesLockCheck()
    {
        // The new resource doesn't have a prior lock; lock check is unnecessary and we should
        // pass straight through. (Verifies the decorator doesn't accidentally guard creation.)
        var newFile = new Mock<IWopiFile>().Object;
        _innerMock.Setup(x => x.CreateWopiChildResource<IWopiFile>("parent", "newfile.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFile);

        var result = await CreateDecorator().CreateWopiChildResource<IWopiFile>("parent", "newfile.txt");

        Assert.Same(newFile, result);
        _lockProviderMock.Verify(x => x.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReadOnlyMethods_DoNotConsultLockProvider()
    {
        _innerMock.Setup(x => x.CheckValidName<IWopiFile>("name", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _innerMock.Setup(x => x.GetSuggestedName<IWopiFile>("parent", "name", It.IsAny<CancellationToken>())).ReturnsAsync("suggested");
        _innerMock.SetupGet(x => x.FileNameMaxLength).Returns(123);

        var decorator = CreateDecorator();
        Assert.True(await decorator.CheckValidName<IWopiFile>("name"));
        Assert.Equal("suggested", await decorator.GetSuggestedName<IWopiFile>("parent", "name"));
        Assert.Equal(123, decorator.FileNameMaxLength);

        _lockProviderMock.Verify(x => x.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void AddWopiLockAwareWritableStorage_WithoutPriorRegistration_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddWopiLockAwareWritableStorage());
        Assert.Contains("IWopiWritableStorageProvider", ex.Message);
    }

    [Fact]
    public async Task AddWopiLockAwareWritableStorage_DecoratesExistingRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_innerMock.Object);
        services.AddSingleton(_lockProviderMock.Object);
        services.AddWopiLockAwareWritableStorage();

        // Existing test rig: when the resource is locked the resolved (decorated) writable
        // provider must throw rather than silently delegate to the inner.
        _lockProviderMock.Setup(x => x.GetLockAsync("file-x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { FileId = "file-x", LockId = "L" });

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IWopiWritableStorageProvider>();

        Assert.IsType<WopiLockAwareWritableStorageProvider>(resolved);
        await Assert.ThrowsAsync<WopiResourceLockedException>(
            () => resolved.DeleteWopiResource<IWopiFile>("file-x"));
    }
}
