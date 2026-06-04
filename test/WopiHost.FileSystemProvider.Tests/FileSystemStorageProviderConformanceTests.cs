using FakeItEasy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions.Testing;

namespace WopiHost.FileSystemProvider.Tests;

/// <summary>
/// Runs the shared <see cref="StorageProviderConformanceTests"/> against <see cref="WopiFileSystemProvider"/>.
/// </summary>
public sealed class FileSystemStorageProviderConformanceTests : StorageProviderConformanceTests
{
    /// <inheritdoc />
    protected override IStorageProviderTestFactory Factory { get; } = new FileSystemFactory();

    private sealed class FileSystemFactory : IStorageProviderTestFactory
    {
        public Task<IStorageProviderTestContext> CreateAsync()
        {
            // Fresh temp directory + id map per call so providers don't share state.
            var root = Directory.CreateTempSubdirectory("FsConformance_");
            var env = A.Fake<IHostEnvironment>();
            A.CallTo(() => env.ContentRootPath).Returns(root.FullName);
            var options = Options.Create(new WopiFileSystemProviderOptions { RootPath = root.FullName });

            var provider = new WopiFileSystemProvider(
                new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance),
                env,
                options,
                NullLogger<WopiFileSystemProvider>.Instance);

            var context = new StorageProviderTestContext(provider, provider, () =>
            {
                root.Refresh();
                if (root.Exists) root.Delete(recursive: true);
                return ValueTask.CompletedTask;
            });
            return Task.FromResult<IStorageProviderTestContext>(context);
        }
    }
}
