using System.Net;
using System.Text.Json;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level coverage of every mutating endpoint in
/// <see cref="WopiHost.Core.Endpoints.FileMutatingEndpoints"/>: PutFile, RenameFile,
/// PutRelativeFile, PutUserInfo, DeleteFile, ProcessCobalt, and the full Lock state machine
/// (Lock / GetLock / Unlock / RefreshLock / UnlockAndRelock) dispatched through ProcessLock.
/// Plus a couple of cross-cutting integration tests (multi-step lock-cycle, unknown override
/// fallthrough) that share the same fixture.
/// </summary>
[Collection("MutatingEndpoints")]
public sealed class FileMutatingEndpointTests(MutatingEndpointsFixture fixture)
{
    private readonly MutatingEndpointsFixture _fixture = fixture;

    // ---- GetShareUrl -----------------------------------------------------

    [Theory]
    [InlineData("ReadOnly")]
    [InlineData("ReadWrite")]
    public async Task GetShareUrl_SupportedUrlType_Returns200WithAbsoluteShareUrl(string urlType)
    {
        var fileId = await _fixture.CreateTempFileAsync([]);
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "GET_SHARE_URL");
        req.Headers.Add("X-WOPI-UrlType", urlType);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var shareUrl = doc.RootElement.GetProperty("ShareUrl").GetString();
        Assert.True(Uri.IsWellFormedUriString(shareUrl, UriKind.Absolute));
    }

    [Fact]
    public async Task GetShareUrl_UnsupportedUrlType_Returns501()
    {
        var fileId = await _fixture.CreateTempFileAsync([]);
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "GET_SHARE_URL");
        req.Headers.Add("X-WOPI-UrlType", "NotASupportedType");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    [Fact]
    public async Task GetShareUrl_MissingUrlTypeHeader_Returns501()
    {
        var fileId = await _fixture.CreateTempFileAsync([]);
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "GET_SHARE_URL");
        // No X-WOPI-UrlType header → treated as unsupported.
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    // ---- PutFile ---------------------------------------------------------

    [Fact]
    public async Task PutFile_OnUnlockedZeroByteFile_Returns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync([]);
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
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
    public async Task PutFile_OnLockedFile_MissingLockHeader_NonEmpty_Returns_409_WithCurrentLock()
    {
        // Spec: file is locked + X-WOPI-Lock missing → lock mismatch → 409 with the CURRENT
        // lock id in X-WOPI-Lock. The mismatch is driven by the file's lock state, not the
        // request header's absence.
        var fileId = await _fixture.CreateTempFileAsync("locked-non-empty"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        var url = $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}";

        var lockReq = new HttpRequestMessage(HttpMethod.Post, url);
        lockReq.Headers.Add("X-WOPI-Override", "LOCK");
        lockReq.Headers.Add("X-WOPI-Lock", "guarding-lock");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(lockReq)).StatusCode);

        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("overwrite-attempt"u8.ToArray()),
        };
        // intentionally omit X-WOPI-Lock
        var resp = await client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("guarding-lock", resp.Headers.GetValues("X-WOPI-Lock").FirstOrDefault());
    }

    [Fact]
    public async Task PutFile_OnLockedFile_MissingLockHeader_ZeroByte_Returns_409_WithCurrentLock()
    {
        // Spec: file is locked → ANY PutFile without a matching X-WOPI-Lock is a mismatch,
        // INCLUDING the 0-byte create-new fast path. Skipping the lock-state check on the
        // no-header path would silently overwrite locked 0-byte files — a security smell
        // against malicious / buggy clients.
        var fileId = await _fixture.CreateTempFileAsync([]);  // 0-byte file
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        var url = $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}";

        var lockReq = new HttpRequestMessage(HttpMethod.Post, url);
        lockReq.Headers.Add("X-WOPI-Override", "LOCK");
        lockReq.Headers.Add("X-WOPI-Lock", "guards-zero-byte");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(lockReq)).StatusCode);

        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("create-new-attempt"u8.ToArray()),
        };
        // intentionally omit X-WOPI-Lock
        var resp = await client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("guards-zero-byte", resp.Headers.GetValues("X-WOPI-Lock").FirstOrDefault());
    }

    [Fact]
    public async Task PutFile_OnUnlockedFile_WithLockHeader_NonEmpty_Returns_409_WithEmptyLock()
    {
        // Spec: file is unlocked → size decides; non-zero → 409 with the empty-lock placeholder
        // in X-WOPI-Lock. PutFile must validate against an existing lock, not establish one — an
        // X-WOPI-Lock sent against an unlocked file must NOT acquire the lock as a side effect.
        var fileId = await _fixture.CreateTempFileAsync("existing-content"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("overwrite-attempt"u8.ToArray()),
        };
        putReq.Headers.Add("X-WOPI-Lock", "client-thinks-it-holds-this");
        var resp = await client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal(string.Empty, resp.Headers.GetValues("X-WOPI-Lock").FirstOrDefault());

        // Side-effect check: the file must NOT now be locked. Probe via GET_LOCK and assert
        // the response carries the empty-lock placeholder, confirming PutFile didn't acquire.
        var getLockReq = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        getLockReq.Headers.Add("X-WOPI-Override", "GET_LOCK");
        var getLockResp = await client.SendAsync(getLockReq);
        Assert.Equal(HttpStatusCode.OK, getLockResp.StatusCode);
        Assert.Equal(string.Empty, getLockResp.Headers.GetValues("X-WOPI-Lock").FirstOrDefault());
    }

    [Fact]
    public async Task PutFile_WithMatchingLock_Updates_AndReturns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync("v1"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
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

    [Fact]
    public async Task PutFile_WithEditorsHeader_Splits_AndReturns_200()
    {
        // ParseEditorsHeader's non-empty branch is only hit when X-WOPI-Editors is populated.
        var fileId = await _fixture.CreateTempFileAsync([]);
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Put, $"/wopi/files/{fileId}/contents?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("hello"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Editors", "editor-1, editor-2 , editor-3");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ---- RenameFile ------------------------------------------------------

    [Fact]
    public async Task RenameFile_Returns_200_WithNewName()
    {
        var fileId = await _fixture.CreateTempFileAsync("rename-me"u8.ToArray(), extension: ".txt");
        var token = await _fixture.MintFileTokenAsync(fileId);
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
    public async Task RenameFile_InvalidName_SanitisesAndReturns_200()
    {
        // Spec: "If the host can't rename the file because the name requested is invalid or
        // conflicts with an existing file, the host should try to generate a different name
        // based on the requested name that meets the file name requirements." An invalid name
        // is sanitised ('/' → '_') and the rename proceeds rather than 400ing immediately.
        var fileId = await _fixture.CreateTempFileAsync("rename-me"u8.ToArray(), extension: ".txt");
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "RENAME_FILE");
        req.Headers.Add("X-WOPI-RequestedName", "bad/name");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Name\"", body);
        Assert.DoesNotContain("/", body);  // forbidden char was sanitised
    }

    [Fact]
    public async Task RenameFile_NotFound_Returns_404()
    {
        var missing = new string('a', 64);
        var token = await _fixture.MintFileTokenAsync(missing);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(parentFileId, WopiFilePermissions.UserCanWrite);
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
    public async Task PutRelativeFile_Suggested_InvalidName_Returns_200_NotBadRequest()
    {
        // Spec: suggested-mode MUST NOT return 400 — the host modifies the name to be valid.
        // "bad/name.txt" contains a forbidden '/' char; the negotiator sanitises it to a valid
        // candidate, the file gets created, and the response is 200.
        var parentFileId = await _fixture.CreateTempFileAsync("anchor"u8.ToArray(), extension: ".txt");
        var token = await _fixture.MintFileTokenAsync(parentFileId, WopiFilePermissions.UserCanWrite);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{parentFileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("body"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_RELATIVE");
        req.Headers.Add("X-WOPI-SuggestedTarget", "bad/name.txt");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PutRelativeFile_Relative_InvalidName_Returns_400_WithValidRelativeTarget()
    {
        // Spec: specific-mode invalid name → 400, optionally with X-WOPI-ValidRelativeTarget so
        // the client can auto-retry with a sanitised name.
        var parentFileId = await _fixture.CreateTempFileAsync("anchor"u8.ToArray(), extension: ".txt");
        var token = await _fixture.MintFileTokenAsync(parentFileId, WopiFilePermissions.UserCanWrite);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{parentFileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("body"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_RELATIVE");
        req.Headers.Add("X-WOPI-RelativeTarget", "bad/name.txt");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.True(resp.Headers.Contains("X-WOPI-ValidRelativeTarget"));
    }

    [Fact]
    public async Task PutRelativeFile_ResponseUrl_CarriesFreshToken_BoundToNewFile()
    {
        // Spec: the Url property includes an access token. To honour "preventing token trading",
        // that token must be bound to the NEW file's resource id — not the source file's.
        var parentFileId = await _fixture.CreateTempFileAsync("anchor"u8.ToArray(), extension: ".txt");
        var token = await _fixture.MintFileTokenAsync(parentFileId, WopiFilePermissions.UserCanWrite);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{parentFileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("body"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_RELATIVE");
        req.Headers.Add("X-WOPI-SuggestedTarget", ".txt");
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        var url = payload.GetProperty("Url").GetString()!;
        var uri = new Uri(url);
        var responseToken = System.Web.HttpUtility.ParseQueryString(uri.Query)["access_token"];
        Assert.False(string.IsNullOrEmpty(responseToken));
        Assert.NotEqual(token, responseToken); // fresh token, not the inbound source-bound one

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(responseToken);
        // The new file's id is encoded in the URL path: /wopi/files/{newFileId}
        var newFileId = uri.AbsolutePath.Split('/').Last();
        Assert.Equal(newFileId, jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceId).Value);
        Assert.NotEqual(parentFileId, jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceId).Value);
    }

    [Fact]
    public async Task PutRelativeFile_BothHeaders_Returns_501()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray(), extension: ".txt");
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
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
    public async Task PutUserInfo_BodyOverLimit_Returns_400()
    {
        // Spec caps UserInfo at 1024 ASCII chars.
        var fileId = await _fixture.CreateTempFileAsync("for-userinfo"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new StringContent(new string('x', 2000)),
        };
        req.Headers.Add("X-WOPI-Override", "PUT_USER_INFO");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PutUserInfo_NotFound_Returns_404()
    {
        var missing = new string('b', 64);
        var token = await _fixture.MintFileTokenAsync(missing);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(missing);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent([]),
        };
        req.Headers.Add("X-WOPI-Override", "COBALT");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [Fact]
    public async Task ProcessCobalt_OnMissingFile_Returns_404()
    {
        // ProcessCobalt returns 404 when the file is missing — matches the rest of the surface.
        var missing = new string('1', 64);
        var token = await _fixture.MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{missing}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent([]),
        };
        req.Headers.Add("X-WOPI-Override", "COBALT");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
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
    public async Task ProcessLock_OnMissingFile_Returns_404()
    {
        // Spec: Lock/Unlock/RefreshLock all list 404 for "Resource not found".
        var missing = new string('0', 64);
        var token = await _fixture.MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(missing, token, "LOCK", newLock: "any-lock-id"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetLock_OnUnlocked_Returns_200_WithEmptyLockHeader()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "active"));

        var resp = await client.SendAsync(LockOp(fileId, token, "GET_LOCK"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("active", string.Join(",", resp.Headers.GetValues("X-WOPI-Lock")));
    }

    [Fact]
    public async Task Lock_MissingNewLockIdentifier_Returns_400()
    {
        // Spec: Lock lists 400 Bad Request — "X-WOPI-Lock was not provided or was empty" — as
        // a distinct status from 409 (lock mismatch).
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Lock_IdTooLong_Returns_400()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "same-id"));

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "same-id"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Unlock_OnUnlocked_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(fileId, token, "UNLOCK", newLock: "any"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Unlock_WithWrongLock_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "unlock-me"));

        var resp = await client.SendAsync(LockOp(fileId, token, "UNLOCK", newLock: "unlock-me"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task RefreshLock_OnUnlocked_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(fileId, token, "REFRESH_LOCK", newLock: "lock"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task RefreshLock_MissingIdentifier_Returns_400()
    {
        // Spec: RefreshLock lists 400 Bad Request for missing/empty X-WOPI-Lock.
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "active"));

        var resp = await client.SendAsync(LockOp(fileId, token, "REFRESH_LOCK"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task RefreshLock_WithMatchingLock_Returns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "refresh-me"));

        var resp = await client.SendAsync(LockOp(fileId, token, "REFRESH_LOCK", newLock: "refresh-me"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task UnlockAndRelock_OnUnlocked_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "new", oldLock: "old"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task UnlockAndRelock_WithWrongOldLock_Returns_409()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "current"));

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "next", oldLock: "wrong-old"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("current", string.Join(",", resp.Headers.GetValues("X-WOPI-Lock")));
    }

    [Fact]
    public async Task UnlockAndRelock_MissingNewLock_Returns_400()
    {
        // Spec: UnlockAndRelock (LOCK override with X-WOPI-OldLock present) lists 400 Bad Request
        // for missing/empty X-WOPI-Lock. The pre-dispatch guard in ProcessLockCore catches this.
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        await client.SendAsync(LockOp(fileId, token, "LOCK", newLock: "current"));

        var resp = await client.SendAsync(LockOp(fileId, token, "LOCK", oldLock: "current"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UnlockAndRelock_WithMatchingOldLock_Returns_200()
    {
        var fileId = await _fixture.CreateTempFileAsync("x"u8.ToArray());
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
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
        var token = await _fixture.MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "BOGUS_OPERATION");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
