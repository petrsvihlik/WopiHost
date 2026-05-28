using System.Net;
using System.Security.Claims;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// Spec-correctness test for the PutRelativeFile 501 path described in #455.
/// </summary>
/// <remarks>
/// <para>
/// <a href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">The PutRelativeFile spec</a>
/// distinguishes "user is unauthorized to call this endpoint" (401/403) from "user is authorized
/// to call this endpoint but not authorized to overwrite the existing target" (501):
/// </para>
/// <para>
/// "If the user is not authorized to overwrite the target file, the host must respond with a
/// 501 Not Implemented."
/// </para>
/// <para>
/// The decision is per-existing-file so it can't live on the access-token's <c>wopi:fperms</c>
/// claim (the existing file isn't known at token-mint time). The contract on
/// <see cref="IWopiPermissionProvider.CanOverwriteFileAsync"/> is the host's seam — return
/// <see langword="false"/> and the negotiator surfaces <see cref="WopiNewChildFileOutcome.NotImplemented"/>
/// which the endpoint translates into <c>501</c>.
/// </para>
/// <para>
/// Has its own backend fixture (not the shared <see cref="MutatingEndpointsFixture"/>) so the
/// deny-overwrite permission provider doesn't taint sibling tests that rely on the default
/// allow-everything behaviour.
/// </para>
/// </remarks>
public sealed class PutRelativeFileOverwrite501Tests : IClassFixture<PutRelativeFileOverwrite501Tests.Fixture>
{
    private const string SharedSigningSecret = "putrelativefile-501-tests-key-32!";
    private readonly Fixture _fixture;

    public PutRelativeFileOverwrite501Tests(Fixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PutRelativeFile_OverwriteExisting_PermissionDenied_Returns_501()
    {
        // Arrange: parent file, plus an EXISTING file in the same container with the name the
        // PUT_RELATIVE call will target. The custom IWopiPermissionProvider registered on the
        // fixture denies CanOverwriteFileAsync for any (user, existingFile) pair, so the
        // overwrite path must surface 501 Not Implemented rather than 200 / 401 / 409.
        var parentFileId = await _fixture.CreateTempFileAsync("anchor"u8.ToArray(), extension: ".txt");
        const string TargetName = "to-be-overwritten.txt";
        await _fixture.CreateSpecificFileAsync(TargetName, "old"u8.ToArray());

        var token = await _fixture.MintFileTokenAsync(parentFileId, WopiFilePermissions.UserCanWrite);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{parentFileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("new-content"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_RELATIVE");
        req.Headers.Add("X-WOPI-RelativeTarget", TargetName);
        req.Headers.Add("X-WOPI-OverwriteRelativeTarget", "true");

        // Act
        var resp = await client.SendAsync(req);

        // Assert: spec-mandated 501.
        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    [Fact]
    public async Task PutRelativeFile_NoExistingTarget_StillSucceeds_When_PermissionProviderDeniesOverwrite()
    {
        // Sanity check: the deny-overwrite provider must NOT affect the new-file path. The 501
        // gate only fires when there's an existing target to overwrite — fresh-name creates
        // continue to return 200. Without this assertion, a future refactor that calls
        // CanOverwriteFileAsync unconditionally would break the create-new flow silently.
        var parentFileId = await _fixture.CreateTempFileAsync("anchor"u8.ToArray(), extension: ".txt");
        var token = await _fixture.MintFileTokenAsync(parentFileId, WopiFilePermissions.UserCanWrite);
        using var client = _fixture.WopiBackend.CreateClient();

        // Use a name we know doesn't exist — GUID-stemmed so it can't collide.
        var freshName = $"fresh-{Guid.NewGuid():N}.txt";
        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{parentFileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("body"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_RELATIVE");
        req.Headers.Add("X-WOPI-RelativeTarget", freshName);
        req.Headers.Add("X-WOPI-OverwriteRelativeTarget", "true");

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    /// <summary>
    /// Backend fixture with an <see cref="IWopiPermissionProvider"/> that denies overwrite —
    /// every other authorization path follows the default permission provider shape.
    /// Isolated from <see cref="MutatingEndpointsFixture"/> so the deny doesn't reach sibling tests.
    /// </summary>
    public sealed class Fixture : IDisposable
    {
        private readonly string _tempRoot;
        public WopiBackendFactory WopiBackend { get; }
        public string RootContainerId { get; }

        public Fixture()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"wopi-pr501-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);

            WopiBackend = new WopiBackendFactory(
                SharedSigningSecret,
                storageRootPath: _tempRoot,
                configureServices: services =>
                {
                    // Replace the default provider with the deny-overwrite variant. AddWopi()
                    // uses TryAddSingleton for IWopiPermissionProvider, so removing + re-adding
                    // is the cleanest way to override.
                    services.RemoveAll<IWopiPermissionProvider>();
                    services.AddSingleton<IWopiPermissionProvider, DenyOverwritePermissionProvider>();
                });

            using var scope = WopiBackend.Services.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
            RootContainerId = storage.RootContainer.Identifier;
        }

        public async Task<string> MintFileTokenAsync(string fileId, WopiFilePermissions permissions)
        {
            using var scope = WopiBackend.Services.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
            var token = await tokens.IssueAsync(new WopiAccessTokenRequest
            {
                UserId = "pr501-user",
                UserDisplayName = "PutRelativeFile 501 User",
                UserEmail = "pr501@example.test",
                ResourceId = fileId,
                ResourceType = WopiResourceType.File,
                FilePermissions = permissions,
            });
            return token.Token;
        }

        public async Task<string> CreateTempFileAsync(byte[] contents, string extension = ".bin")
        {
            var fileName = $"anchor-{Guid.NewGuid():N}{extension}";
            return await CreateSpecificFileAsync(fileName, contents);
        }

        public async Task<string> CreateSpecificFileAsync(string fileName, byte[] contents)
        {
            using var scope = WopiBackend.Services.CreateScope();
            var writable = scope.ServiceProvider.GetRequiredService<IWopiWritableStorageProvider>();
            var file = await writable.CreateWopiChildFile(RootContainerId, fileName)
                ?? throw new InvalidOperationException($"CreateWopiChildFile returned null for {fileName}");
            if (contents.Length > 0)
            {
                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(contents);
            }
            return file.Identifier;
        }

        public void Dispose()
        {
            WopiBackend.Dispose();
            try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
        }

        /// <summary>
        /// Permission provider that allows everything except <see cref="CanOverwriteFileAsync"/>,
        /// which always denies. Mirrors the default permission shape for <c>GetFilePermissionsAsync</c> /
        /// <c>GetContainerPermissionsAsync</c> so the endpoint-level authorization gate still
        /// passes — the only thing this provider changes is the per-existing-file overwrite check.
        /// </summary>
        private sealed class DenyOverwritePermissionProvider : IWopiPermissionProvider
        {
            public Task<WopiFilePermissions> GetFilePermissionsAsync(ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default)
                // Match what the access token claim carries (UserCanWrite). The token-side
                // claim is what's actually consulted by WopiAuthorizationHandler for the
                // Permission.Create gate on PutRelativeFile — this method is called for
                // CheckFileInfo population. Returning the same shape keeps observable
                // behaviour close to DefaultWopiPermissionProvider.
                => Task.FromResult(WopiFilePermissions.UserCanWrite);

            public Task<WopiContainerPermissions> GetContainerPermissionsAsync(ClaimsPrincipal user, IWopiContainer container, CancellationToken cancellationToken = default)
                => Task.FromResult(WopiContainerPermissions.UserCanCreateChildFile | WopiContainerPermissions.UserCanCreateChildContainer);

            public Task<bool> CanOverwriteFileAsync(ClaimsPrincipal user, IWopiFile existingFile, CancellationToken cancellationToken = default)
                => Task.FromResult(false);
        }
    }
}
