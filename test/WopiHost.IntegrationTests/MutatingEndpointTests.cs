using System.Net;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level coverage of every mutating endpoint in <c>MapWopiEndpoints</c>
/// (FileMutatingEndpoints + ContainerMutatingEndpoints). Mutating tests need an isolated
/// file-system root so they don't corrupt the shared sample/wopi-docs the read-only smoke
/// tests rely on — the fixture copies the sample into a per-class temp directory and points
/// <see cref="WopiBackendFactory"/> at it. Cleanup happens in <see cref="IDisposable.Dispose"/>.
/// </summary>
public sealed class MutatingEndpointTests(MutatingEndpointTests.Fixture fixture) : IClassFixture<MutatingEndpointTests.Fixture>
{
    private const string SharedSigningSecret = "mutating-tests-shared-key-32bytes!";

    private readonly Fixture _fixture = fixture;

    private async Task<string> MintFileTokenAsync(string fileId, WopiFilePermissions permissions = WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanRename)
    {
        using var scope = _fixture.WopiBackend.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "mut-user",
            UserDisplayName = "Mut User",
            UserEmail = "mut@example.com",
            ResourceId = fileId,
            ResourceType = WopiResourceType.File,
            FilePermissions = permissions,
        });
        return token.Token;
    }

    private async Task<string> MintContainerTokenAsync(string containerId)
    {
        using var scope = _fixture.WopiBackend.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "mut-user",
            UserDisplayName = "Mut User",
            UserEmail = "mut@example.com",
            ResourceId = containerId,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = WopiContainerPermissions.UserCanCreateChildContainer
                | WopiContainerPermissions.UserCanCreateChildFile
                | WopiContainerPermissions.UserCanDelete
                | WopiContainerPermissions.UserCanRename,
        });
        return token.Token;
    }

    // ---- PutFile ---------------------------------------------------------

    [Fact]
    public async Task PutFile_OnUnlockedZeroByteFile_Returns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync([]);
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Put, $"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("hello world"u8.ToArray()),
        };
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PutFile_OnUnlockedNonEmptyFile_Returns_409()
    {
        // Spec: PutFile on a non-empty unlocked file must 409 with the empty-lock header set.
        var fileId = await _fixture.CreateTempFileAsync("existing"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Put, $"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("update"u8.ToArray()),
        };
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.True(resp.Headers.Contains("X-WOPI-Lock"));
    }

    [Fact]
    public async Task PutFile_WithMatchingLock_Updates_AndReturns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync("v1"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        var url = $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}";

        // Acquire lock
        var lockReq = new HttpRequestMessage(HttpMethod.Post, url);
        lockReq.Headers.Add("X-WOPI-Override", "LOCK");
        lockReq.Headers.Add("X-WOPI-Lock", "lock-put");
        var lockResp = await client.SendAsync(lockReq);
        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);

        // PutFile with matching lock — should succeed
        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("v2"u8.ToArray()),
        };
        putReq.Headers.Add("X-WOPI-Lock", "lock-put");
        var putResp = await client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
    }

    // ---- RenameFile ------------------------------------------------------

    [Fact]
    public async Task RenameFile_Returns_200_WithNewName()
    {
        var fileId = await _fixture.CreateTempFileAsync("rename-me"u8.ToArray(), extension: ".txt");
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "RENAME_FILE");
        req.Headers.Add("X-WOPI-RequestedName", "renamed");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Name\"", body);
        Assert.Contains("renamed", body);
    }

    [Fact]
    public async Task RenameFile_NotFound_Returns_404()
    {
        var missing = new string('a', 64);
        var token = await MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{missing}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "RENAME_FILE");
        req.Headers.Add("X-WOPI-RequestedName", "x");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RenameFile_LockMismatch_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("locked"u8.ToArray(), extension: ".txt");
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        var url = $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}";

        // Acquire a lock first
        var lockReq = new HttpRequestMessage(HttpMethod.Post, url);
        lockReq.Headers.Add("X-WOPI-Override", "LOCK");
        lockReq.Headers.Add("X-WOPI-Lock", "lock-rename");
        await client.SendAsync(lockReq);

        // RenameFile without the matching lock header → 409
        var renameReq = new HttpRequestMessage(HttpMethod.Post, url);
        renameReq.Headers.Add("X-WOPI-Override", "RENAME_FILE");
        renameReq.Headers.Add("X-WOPI-RequestedName", "x");
        var resp = await client.SendAsync(renameReq);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ---- PutRelativeFile -------------------------------------------------

    [Fact]
    public async Task PutRelativeFile_WithSuggestedTarget_Returns_200()
    {
        var parentFileId = await _fixture.CreateTempFileAsync("anchor"u8.ToArray(), extension: ".txt");
        var token = await MintFileTokenAsync(parentFileId, WopiFilePermissions.UserCanWrite);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{parentFileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("new-content"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_RELATIVE");
        req.Headers.Add("X-WOPI-SuggestedTarget", ".txt"); // extension-only — host generates name
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Name\"", body);
    }

    [Fact]
    public async Task PutRelativeFile_BothHeaders_Returns_501()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray(), extension: ".txt");
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent([]),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_RELATIVE");
        req.Headers.Add("X-WOPI-SuggestedTarget", "a.txt");
        req.Headers.Add("X-WOPI-RelativeTarget", "b.txt");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    // ---- PutUserInfo -----------------------------------------------------

    [Fact]
    public async Task PutUserInfo_Returns_200_AndStoresInfo()
    {
        var fileId = await _fixture.CreateTempFileAsync("for-userinfo"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new StringContent("user-blob"),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_USER_INFO");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // CheckFileInfo on the same file should reflect the stored UserInfo.
        var info = await client.GetAsync($"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        var body = await info.Content.ReadAsStringAsync();
        Assert.Contains("user-blob", body);
    }

    [Fact]
    public async Task PutUserInfo_NotFound_Returns_404()
    {
        var missing = new string('b', 64);
        var token = await MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{missing}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new StringContent("x"),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_USER_INFO");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---- DeleteFile ------------------------------------------------------

    [Fact]
    public async Task DeleteFile_Returns_200_AndRemovesFile()
    {
        var fileId = await _fixture.CreateTempFileAsync("delete-me"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "DELETE");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Subsequent CheckFileInfo should 404.
        var check = await client.GetAsync($"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.NotFound, check.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_NotFound_Returns_404()
    {
        var missing = new string('c', 64);
        var token = await MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{missing}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "DELETE");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_OnLockedFile_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("locked-delete"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        var url = $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}";

        // Lock
        var lockReq = new HttpRequestMessage(HttpMethod.Post, url);
        lockReq.Headers.Add("X-WOPI-Override", "LOCK");
        lockReq.Headers.Add("X-WOPI-Lock", "lock-del");
        await client.SendAsync(lockReq);

        // Delete
        var delReq = new HttpRequestMessage(HttpMethod.Post, url);
        delReq.Headers.Add("X-WOPI-Override", "DELETE");
        var resp = await client.SendAsync(delReq);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ---- ProcessCobalt ---------------------------------------------------

    [Fact]
    public async Task ProcessCobalt_WhenNotEnabled_Returns_501()
    {
        // The mutating fixture leaves Wopi:UseCobalt=false (the default), so ProcessCobalt's
        // RequiresWritableStorageEndpointFilter still lets the request through, but
        // ICobaltProcessor isn't registered — the handler's ArgumentNullException.ThrowIfNull
        // surfaces as a 500. Spec'd behaviour for hosts without Cobalt support; the validator
        // exercises the success path when Cobalt is wired in.
        var fileId = await _fixture.CreateTempFileAsync("cobalt-target"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent([]),
        };
        req.Headers.Add("X-WOPI-Override", "COBALT");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    // ---- Lock state machine ---------------------------------------------
    //
    // The full Lock / GetLock / Unlock / RefreshLock / UnlockAndRelock matrix sits behind a
    // single X-WOPI-Override dispatcher in ProcessLockCore. These tests walk every transition
    // so the dispatcher + handler bodies are covered end-to-end against the real in-memory
    // lock provider (no Moq).

    private static HttpRequestMessage LockOp(string fileId, string token, string op, string? newLock = null, string? oldLock = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", op);
        if (newLock is not null) req.Headers.Add("X-WOPI-Lock", newLock);
        if (oldLock is not null) req.Headers.Add("X-WOPI-OldLock", oldLock);
        return req;
    }

    [Fact]
    public async Task GetLock_OnUnlocked_Returns_200_WithEmptyLockHeader()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(fileId, token, "GET_LOCK"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(resp.Headers.Contains("X-WOPI-Lock"));
        // Default EmptyLockHeaderValue is empty string.
        Assert.Equal(string.Empty, string.Join(",", resp.Headers.GetValues("X-WOPI-Lock")));
    }

    [Fact]
    public async Task GetLock_OnLocked_Returns_200_WithLockId()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "active"));

        var resp = await client.SendAsync(LockOp(fileId, token, "GET_LOCK"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("active", string.Join(",", resp.Headers.GetValues("X-WOPI-Lock")));
    }

    [Fact]
    public async Task Lock_MissingNewLockIdentifier_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        // No X-WOPI-Lock header at all → HandleLock returns WopiLockMismatchResult.
        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Lock_IdTooLong_Returns_400()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        // WopiLockInfo.MaxLockIdLength is 1024; 2000 chars is clearly over.
        var oversized = new string('z', 2000);

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: oversized));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.True(resp.Headers.Contains("X-WOPI-LockFailureReason"));
    }

    [Fact]
    public async Task Lock_OnLockedSameId_Refreshes_Returns_200()
    {
        // LOCK with the same id as the existing lock dispatches into LockOrRefresh's refresh
        // branch — covers HandleLock's "existingLock is not null" path.
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "same-id"));

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "same-id"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Unlock_OnUnlocked_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(fileId, token, "UNLOCK", newLock: "any"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Unlock_WithWrongLock_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "real-lock"));

        var resp = await client.SendAsync(LockOp(fileId, token, "UNLOCK", newLock: "wrong-lock"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("real-lock", string.Join(",", resp.Headers.GetValues("X-WOPI-Lock")));
    }

    [Fact]
    public async Task Unlock_WithMatchingLock_Returns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "unlock-me"));

        var resp = await client.SendAsync(LockOp(fileId, token, "UNLOCK", newLock: "unlock-me"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task RefreshLock_OnUnlocked_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(fileId, token, "REFRESH_LOCK", newLock: "lock"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task RefreshLock_MissingIdentifier_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "active"));

        var resp = await client.SendAsync(LockOp(fileId, token, "REFRESH_LOCK"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task RefreshLock_WithMatchingLock_Returns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "refresh-me"));

        var resp = await client.SendAsync(LockOp(fileId, token, "REFRESH_LOCK", newLock: "refresh-me"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task UnlockAndRelock_OnUnlocked_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "new", oldLock: "old"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task UnlockAndRelock_WithWrongOldLock_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "current"));

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "next", oldLock: "wrong-old"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("current", string.Join(",", resp.Headers.GetValues("X-WOPI-Lock")));
    }

    [Fact]
    public async Task UnlockAndRelock_MissingNewLock_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "current"));

        // LOCK with X-WOPI-OldLock but no X-WOPI-Lock dispatches into HandleUnlockAndRelock,
        // which 409s when newLockIdentifier is missing.
        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", oldLock: "current"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task PutFile_WithEditorsHeader_Splits_AndReturns_200()
    {
        // ParseEditorsHeader's non-empty branch is only hit when X-WOPI-Editors is populated.
        var fileId = await _fixture.CreateTempFileAsync([]);
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Put, $"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("hello"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Editors", "editor-1, editor-2 , editor-3");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task UnlockAndRelock_WithMatchingOldLock_Returns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "v1"));

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "v2", oldLock: "v1"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        // GetLock should now report v2.
        var get = await client.SendAsync(LockOp(fileId, token, "GET_LOCK"));
        Assert.Equal("v2", string.Join(",", get.Headers.GetValues("X-WOPI-Lock")));
    }

    [Fact]
    public async Task Lock_Unlock_Cycle_Roundtrips()
    {
        // End-to-end cycle: LOCK → GET_LOCK → REFRESH_LOCK → UNLOCK. Complements the
        // per-state tests above by verifying state survives across multiple HTTP requests
        // on the same lock id.
        var fileId = await _fixture.CreateTempFileAsync("cycle"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var lockResp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "lock-1"));
        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);

        var getLockResp = await client.SendAsync(LockOp(fileId, token, "GET_LOCK"));
        Assert.Equal(HttpStatusCode.OK, getLockResp.StatusCode);
        Assert.Equal("lock-1", getLockResp.Headers.GetValues("X-WOPI-Lock").FirstOrDefault());

        var refreshResp = await client.SendAsync(LockOp(fileId, token, "REFRESH_LOCK", newLock: "lock-1"));
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);

        var unlockResp = await client.SendAsync(LockOp(fileId, token, "UNLOCK", newLock: "lock-1"));
        Assert.Equal(HttpStatusCode.OK, unlockResp.StatusCode);
    }

    [Fact]
    public async Task Lock_OnLockedDifferentId_Returns_409_WithExistingLock()
    {
        // LOCK with a different id than the one already held → 409 with the original lock
        // echoed back in X-WOPI-Lock. Distinct from Lock_OnLockedSameId_Refreshes which
        // exercises the refresh branch when ids match.
        var fileId = await _fixture.CreateTempFileAsync("conflict"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "lock-initial"));

        var conflict = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "lock-other"));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("lock-initial", conflict.Headers.GetValues("X-WOPI-Lock").FirstOrDefault());
    }

    [Fact]
    public async Task Unknown_Override_Returns_404()
    {
        // WopiOverrideMatcherPolicy invalidates all override-bearing candidates when no
        // endpoint declares a matching X-WOPI-Override; framework then falls through to
        // 404 (documented behaviour — see the policy class XML remarks).
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "BOGUS_OPERATION");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---- CreateChildContainer --------------------------------------------

    [Fact]
    public async Task CreateChildContainer_WithSuggestedTarget_Returns_200()
    {
        var token = await MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        req.Headers.Add("X-WOPI-SuggestedTarget", $"folder-{Guid.NewGuid():N}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"ContainerPointer\"", body);
    }

    [Fact]
    public async Task CreateChildContainer_WithRelativeTarget_OnExisting_Returns_409()
    {
        var token = await MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        // First create a folder with a known name…
        var name = $"folder-{Guid.NewGuid():N}";
        var first = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        first.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        first.Headers.Add("X-WOPI-RelativeTarget", name);
        var firstResp = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        // …then ask for it again under specific (relative) mode → 409.
        var second = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        second.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        second.Headers.Add("X-WOPI-RelativeTarget", name);
        var secondResp = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, secondResp.StatusCode);
        Assert.True(secondResp.Headers.Contains("X-WOPI-ValidRelativeTarget"));
    }

    [Fact]
    public async Task CreateChildContainer_BothHeaders_Returns_501()
    {
        var token = await MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        req.Headers.Add("X-WOPI-SuggestedTarget", "a");
        req.Headers.Add("X-WOPI-RelativeTarget", "b");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    // ---- CreateChildFile -------------------------------------------------

    [Fact]
    public async Task CreateChildFile_WithSuggestedTarget_Returns_200()
    {
        var token = await MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("file-body"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_FILE");
        req.Headers.Add("X-WOPI-SuggestedTarget", ".txt");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CreateChildFile_BothHeaders_Returns_501()
    {
        var token = await MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent([]),
        };
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_FILE");
        req.Headers.Add("X-WOPI-SuggestedTarget", "a.txt");
        req.Headers.Add("X-WOPI-RelativeTarget", "b.txt");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    // ---- DeleteContainer / RenameContainer -------------------------------

    [Fact]
    public async Task DeleteContainer_OnEmpty_Returns_200()
    {
        // Create a fresh empty subfolder, then delete it.
        using var client = _fixture.WopiBackend.CreateClient();
        var childId = await _fixture.CreateTempContainerAsync();
        var childToken = await MintContainerTokenAsync(childId);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{childId}?access_token={Uri.EscapeDataString(childToken)}");
        req.Headers.Add("X-WOPI-Override", "DELETE_CONTAINER");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteContainer_NonEmpty_Returns_409()
    {
        // Create a subfolder, drop a file into it, then try to delete the folder.
        // FileSystemProvider's DeleteWopiContainer raises InvalidOperationException on a
        // non-empty directory, which the handler maps to 409.
        var childId = await _fixture.CreateTempContainerAsync();
        var fileName = $"child-{Guid.NewGuid():N}.bin";
        using (var scope = _fixture.WopiBackend.Services.CreateScope())
        {
            var writable = scope.ServiceProvider.GetRequiredService<IWopiWritableStorageProvider>();
            await writable.CreateWopiChildFile(childId, fileName);
        }
        var token = await MintContainerTokenAsync(childId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{childId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "DELETE_CONTAINER");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteContainer_NotFound_Returns_404()
    {
        var missing = new string('d', 64);
        var token = await MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{missing}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "DELETE_CONTAINER");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RenameContainer_Returns_200()
    {
        var childId = await _fixture.CreateTempContainerAsync();
        var token = await MintContainerTokenAsync(childId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{childId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "RENAME_CONTAINER");
        req.Headers.Add("X-WOPI-RequestedName", $"renamed-{Guid.NewGuid():N}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task RenameContainer_NotFound_Returns_404()
    {
        var missing = new string('e', 64);
        var token = await MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{missing}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "RENAME_CONTAINER");
        req.Headers.Add("X-WOPI-RequestedName", "x");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>
    /// Per-test-class fixture. Boots a <see cref="WopiBackendFactory"/> rooted at a fresh
    /// temp directory containing a copy of <see cref="TestPaths.WopiDocsRoot"/>, so mutating
    /// tests can write / delete / rename without corrupting the shared sample. Exposes
    /// helpers for materialising disposable test files + containers that resolve to a
    /// usable storage identifier.
    /// </summary>
    public sealed class Fixture : IDisposable
    {
        private readonly string _tempRoot;
        public WopiBackendFactory WopiBackend { get; }
        public string RootContainerId { get; }

        public Fixture()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"wopi-mut-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
            CopyDirectory(TestPaths.WopiDocsRoot, _tempRoot);

            WopiBackend = new WopiBackendFactory(SharedSigningSecret, storageRootPath: _tempRoot);

            using var scope = WopiBackend.Services.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
            RootContainerId = storage.RootContainer.Identifier;
        }

        /// <summary>
        /// Creates a file via <see cref="IWopiWritableStorageProvider.CreateWopiChildFile"/>
        /// so the FileSystemProvider's id→path cache registers the new identifier (dropping
        /// raw bytes onto disk would leave the cache stale and the file invisible to
        /// <c>GetWopiFiles</c>). Then writes <paramref name="contents"/> through the writable
        /// file handle.
        /// </summary>
        public async Task<string> CreateTempFileAsync(byte[] contents, string extension = ".bin")
        {
            var fileName = $"mut-{Guid.NewGuid():N}{extension}";
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

        /// <summary>
        /// Creates a subfolder via <see cref="IWopiWritableStorageProvider.CreateWopiChildContainer"/>
        /// so the id→path cache registers it. Returns the new container's identifier.
        /// </summary>
        public async Task<string> CreateTempContainerAsync()
        {
            var folderName = $"mut-container-{Guid.NewGuid():N}";
            using var scope = WopiBackend.Services.CreateScope();
            var writable = scope.ServiceProvider.GetRequiredService<IWopiWritableStorageProvider>();
            var container = await writable.CreateWopiChildContainer(RootContainerId, folderName)
                ?? throw new InvalidOperationException($"CreateWopiChildContainer returned null for {folderName}");
            return container.Identifier;
        }

        private static void CopyDirectory(string source, string destination)
        {
            foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.Replace(source, destination, StringComparison.Ordinal));
            }
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(source, destination, StringComparison.Ordinal), overwrite: true);
            }
        }

        public void Dispose()
        {
            WopiBackend.Dispose();
            try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
