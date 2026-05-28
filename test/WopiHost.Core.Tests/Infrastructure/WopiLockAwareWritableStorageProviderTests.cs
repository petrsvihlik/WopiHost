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
        _innerMock.Setup(x => x.DeleteWopiFile("file-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateDecorator().DeleteWopiFile("file-1");

        Assert.True(result);
        _innerMock.Verify(x => x.DeleteWopiFile("file-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteWopiResource_Locked_ThrowsAndDoesNotDelegate()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("file-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { FileId = "file-1", LockId = "active-lock" });

        var ex = await Assert.ThrowsAsync<WopiResourceLockedException>(
            () => CreateDecorator().DeleteWopiFile("file-1"));
        Assert.Equal("file-1", ex.ResourceIdentifier);
        Assert.Equal("active-lock", ex.LockId);
        _innerMock.Verify(x => x.DeleteWopiFile(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RenameWopiResource_NoLock_DelegatesToInner()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("file-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WopiLockInfo?)null);
        _innerMock.Setup(x => x.RenameWopiFile("file-2", "renamed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateDecorator().RenameWopiFile("file-2", "renamed");

        Assert.True(result);
        _innerMock.Verify(x => x.RenameWopiFile("file-2", "renamed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenameWopiResource_Locked_ThrowsAndDoesNotDelegate()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("file-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { FileId = "file-2", LockId = "lock-x" });

        await Assert.ThrowsAsync<WopiResourceLockedException>(
            () => CreateDecorator().RenameWopiFile("file-2", "renamed"));
        _innerMock.Verify(x => x.RenameWopiFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateWopiChildResource_BypassesLockCheck()
    {
        // The new resource doesn't have a prior lock; lock check is unnecessary and we should
        // pass straight through. (Verifies the decorator doesn't accidentally guard creation.)
        var newFile = new Mock<IWopiWritableFile>().Object;
        _innerMock.Setup(x => x.CreateWopiChildFile("parent", "newfile.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFile);

        var result = await CreateDecorator().CreateWopiChildFile("parent", "newfile.txt");

        Assert.Same(newFile, result);
        _lockProviderMock.Verify(x => x.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReadOnlyMethods_DoNotConsultLockProvider()
    {
        _innerMock.Setup(x => x.CheckValidFileName("name", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _innerMock.Setup(x => x.CheckValidContainerName("cname", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _innerMock.Setup(x => x.GetSuggestedFileName("parent", "name", It.IsAny<CancellationToken>())).ReturnsAsync("suggested");
        _innerMock.Setup(x => x.GetSuggestedContainerName("parent", "cname", It.IsAny<CancellationToken>())).ReturnsAsync("suggested-c");
        _innerMock.SetupGet(x => x.FileNameMaxLength).Returns(123);

        var decorator = CreateDecorator();
        Assert.True(await decorator.CheckValidFileName("name"));
        Assert.True(await decorator.CheckValidContainerName("cname"));
        Assert.Equal("suggested", await decorator.GetSuggestedFileName("parent", "name"));
        Assert.Equal("suggested-c", await decorator.GetSuggestedContainerName("parent", "cname"));
        Assert.Equal(123, decorator.FileNameMaxLength);

        _lockProviderMock.Verify(x => x.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetWritableFile_DelegatesToInner()
    {
        // Plain pass-through — there's no lock to consult for an opaque "give me a writable
        // handle" request; the lock check belongs at the call site that mutates content.
        var file = Mock.Of<IWopiWritableFile>();
        _innerMock.Setup(x => x.GetWritableFile("file-x", It.IsAny<CancellationToken>())).ReturnsAsync(file);

        var result = await CreateDecorator().GetWritableFile("file-x");

        Assert.Same(file, result);
        _lockProviderMock.Verify(x => x.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateWopiChildContainer_BypassesLockCheck()
    {
        // Mirror of the file-creation test: a brand-new container has no prior lock.
        var newContainer = Mock.Of<IWopiContainer>();
        _innerMock.Setup(x => x.CreateWopiChildContainer("parent", "subdir", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newContainer);

        var result = await CreateDecorator().CreateWopiChildContainer("parent", "subdir");

        Assert.Same(newContainer, result);
        _lockProviderMock.Verify(x => x.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWopiContainer_NoLock_DelegatesToInner()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("c-1", It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _innerMock.Setup(x => x.DeleteWopiContainer("c-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateDecorator().DeleteWopiContainer("c-1");

        Assert.True(result);
        _innerMock.Verify(x => x.DeleteWopiContainer("c-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteWopiContainer_Locked_Throws()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("c-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { FileId = "c-1", LockId = "L" });

        var ex = await Assert.ThrowsAsync<WopiResourceLockedException>(
            () => CreateDecorator().DeleteWopiContainer("c-1"));
        Assert.Equal("c-1", ex.ResourceIdentifier);
        Assert.Equal("L", ex.LockId);
        _innerMock.Verify(x => x.DeleteWopiContainer(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RenameWopiContainer_NoLock_DelegatesToInner()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("c-2", It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _innerMock.Setup(x => x.RenameWopiContainer("c-2", "renamed", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateDecorator().RenameWopiContainer("c-2", "renamed");

        Assert.True(result);
        _innerMock.Verify(x => x.RenameWopiContainer("c-2", "renamed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenameWopiContainer_Locked_Throws()
    {
        _lockProviderMock.Setup(x => x.GetLockAsync("c-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { FileId = "c-2", LockId = "L-c" });

        await Assert.ThrowsAsync<WopiResourceLockedException>(
            () => CreateDecorator().RenameWopiContainer("c-2", "renamed"));
        _innerMock.Verify(x => x.RenameWopiContainer(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_NullInner_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WopiLockAwareWritableStorageProvider(null!, _lockProviderMock.Object));

    [Fact]
    public void Constructor_NullLockProvider_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WopiLockAwareWritableStorageProvider(_innerMock.Object, null!));

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
            () => resolved.DeleteWopiFile("file-x"));
    }

    [Fact]
    public async Task AddWopiLockAwareWritableStorage_PreservesFactoryRegistration()
    {
        // Covers the ImplementationFactory branch of the decorator-wiring code: when the inner
        // registration is `services.AddSingleton<IWopiWritableStorageProvider>(sp => ...)` the
        // wiring code must call the factory (not new up the type, which there isn't one for).
        var inner = _innerMock.Object;
        var services = new ServiceCollection();
        services.AddSingleton<IWopiWritableStorageProvider>(_ => inner);
        services.AddSingleton(_lockProviderMock.Object);
        services.AddWopiLockAwareWritableStorage();

        _lockProviderMock.Setup(x => x.GetLockAsync("file-y", It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _innerMock.Setup(x => x.DeleteWopiFile("file-y", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IWopiWritableStorageProvider>();

        Assert.IsType<WopiLockAwareWritableStorageProvider>(resolved);
        Assert.True(await resolved.DeleteWopiFile("file-y"));
        // The factory must have been invoked exactly once when the decorated registration was resolved.
        _innerMock.Verify(x => x.DeleteWopiFile("file-y", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AddWopiLockAwareWritableStorage_ActivatesImplementationType()
    {
        // Covers the ActivatorUtilities branch: when the inner is registered as
        // `AddSingleton<IWopiWritableStorageProvider, ConcreteType>()` neither
        // ImplementationInstance nor ImplementationFactory is set, and the wiring must reach
        // for ActivatorUtilities to construct the concrete type from DI.
        var services = new ServiceCollection();
        services.AddSingleton<IWopiWritableStorageProvider, DummyWritableStorageProvider>();
        services.AddSingleton(_lockProviderMock.Object);
        services.AddWopiLockAwareWritableStorage();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IWopiWritableStorageProvider>();

        // Outer wrapper is the decorator; the inner one (visible behind delegation) is the dummy
        // class. Asserting the wrapper type proves the decorator was wired correctly via the
        // ActivatorUtilities branch.
        Assert.IsType<WopiLockAwareWritableStorageProvider>(resolved);
    }

    private sealed class DummyWritableStorageProvider : IWopiWritableStorageProvider
    {
        public int FileNameMaxLength => 250;
        public Task<IWopiWritableFile?> CreateWopiChildFile(string containerId, string name, CancellationToken cancellationToken = default) => Task.FromResult<IWopiWritableFile?>(null);
        public Task<IWopiContainer?> CreateWopiChildContainer(string containerId, string name, CancellationToken cancellationToken = default) => Task.FromResult<IWopiContainer?>(null);
        public Task<IWopiWritableFile?> GetWritableFile(string identifier, CancellationToken cancellationToken = default) => Task.FromResult<IWopiWritableFile?>(null);
        public Task<bool> DeleteWopiFile(string identifier, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RenameWopiFile(string identifier, string requestedName, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RenameWopiContainer(string identifier, string requestedName, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> CheckValidFileName(string name, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> CheckValidContainerName(string name, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetSuggestedFileName(string containerId, string name, CancellationToken cancellationToken = default) => Task.FromResult(name);
        public Task<string> GetSuggestedContainerName(string containerId, string name, CancellationToken cancellationToken = default) => Task.FromResult(name);
    }
}
