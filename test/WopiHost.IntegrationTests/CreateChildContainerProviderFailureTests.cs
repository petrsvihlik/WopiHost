using System.Net;
using FakeItEasy;
using WopiHost.Abstractions;
using WopiHost.FileSystemProvider;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// Covers the <c>CreateChildContainer</c> branch where the storage provider returns no container:
/// the endpoint answers <c>500 Internal Server Error</c> rather than a conflict or a success body.
/// </summary>
public sealed class CreateChildContainerProviderFailureTests : IClassFixture<CreateChildContainerProviderFailureTests.Fixture>
{
    private static readonly FixtureUser s_user = new("ccc-fail-user", "Create Failure User", "ccc-fail@example.test");
    private readonly Fixture _fixture;

    public CreateChildContainerProviderFailureTests(Fixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("X-WOPI-SuggestedTarget")]
    [InlineData("X-WOPI-RelativeTarget")]
    public async Task CreateChildContainer_ProviderReturnsNull_Returns_500(string targetHeader)
    {
        var token = await FixtureTokens.MintContainerTokenAsync(_fixture.WopiBackend, s_user, _fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        req.Headers.Add(targetHeader, $"folder-{Guid.NewGuid():N}");
        var resp = await client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    /// <summary>
    /// Provides a backend whose writable storage wraps the file-system provider but returns
    /// <see langword="null"/> from <see cref="IWopiWritableStorageProvider.CreateWopiChildContainer"/>.
    /// </summary>
    public sealed class Fixture : IDisposable
    {
        private readonly string _tempRoot;

        /// <summary>Initializes a new instance of the <see cref="Fixture"/> class.</summary>
        public Fixture()
        {
            _tempRoot = Path.Join(Path.GetTempPath(), $"wopi-ccc-fail-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);

            WopiBackend = new WopiBackendFactory(
                "create-child-container-fail-key!!",
                storageRootPath: _tempRoot,
                configureServices: services => services.AddSingleton(sp =>
                {
                    var real = sp.GetRequiredService<WopiFileSystemProvider>();
                    var writable = A.Fake<IWopiWritableStorageProvider>(o => o.Wrapping(real));
                    A.CallTo(() => writable.CreateWopiChildContainer(A<string>._, A<string>._, A<CancellationToken>._))
                        .Returns(Task.FromResult<IWopiContainer?>(null));
                    return writable;
                }));

            using var scope = WopiBackend.Services.CreateScope();
            RootContainerId = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>().RootContainer.Identifier;
        }

        /// <summary>Gets the backend under test.</summary>
        public WopiBackendFactory WopiBackend { get; }

        /// <summary>Gets the identifier of the storage root container.</summary>
        public string RootContainerId { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            WopiBackend.Dispose();
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup of the temp storage root.
            }
        }
    }
}
