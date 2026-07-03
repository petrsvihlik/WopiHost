using System.Net;
using System.Text.Json;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// CheckFileInfo capability advertising on a host without writable storage: SupportsUpdate,
/// SupportsRename, and SupportsDeleteFile must all be false, because PutFile / RenameFile /
/// DeleteFile 501 via <c>RequiresWritableStorageEndpointFilter</c>. A read-only host that
/// advertises them would make a WOPI client render Rename/Delete affordances that always fail.
/// </summary>
public sealed class ReadOnlyHostCheckFileInfoTests : IDisposable
{
    private const string SigningSecret = "readonly-capabilities-shared-key-32b!";
    private static readonly FixtureUser s_user = new("readonly-user", "ReadOnly User", "readonly@example.com");

    private readonly WopiBackendFactory _backend;

    public ReadOnlyHostCheckFileInfoTests()
    {
        _backend = new WopiBackendFactory(SigningSecret, configureServices: services =>
        {
            services.RemoveAll<IWopiWritableStorageProvider>();
            // DefaultWopiNewChildFileNegotiator has a hard constructor dependency on
            // IWopiWritableStorageProvider, so its registration must go too — it is only
            // reachable from endpoints that 501 on a read-only host anyway.
            services.RemoveAll<IWopiNewChildFileNegotiator>();
        });
    }

    [Fact]
    public async Task CheckFileInfo_WithoutWritableStorage_DoesNotAdvertiseUpdateRenameDelete()
    {
        using var scope = _backend.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
        var fileId = await FirstFileIdAsync(storage);
        var token = await FixtureTokens.MintFileTokenAsync(_backend, s_user, fileId, WopiFilePermissions.UserCanWrite);
        using var client = _backend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var payload = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        Assert.False(payload.GetProperty("SupportsUpdate").GetBoolean());
        Assert.False(payload.GetProperty("SupportsRename").GetBoolean());
        Assert.False(payload.GetProperty("SupportsDeleteFile").GetBoolean());
    }

    private static async Task<string> FirstFileIdAsync(IWopiStorageProvider storage)
    {
        await foreach (var f in storage.GetWopiFiles(storage.RootContainer.Identifier))
        {
            return f.Identifier;
        }
        throw new InvalidOperationException("sample/wopi-docs is empty — at least one file is required.");
    }

    public void Dispose() => _backend.Dispose();
}
