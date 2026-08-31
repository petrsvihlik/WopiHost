using System.Text.Json;
using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

public class WopiCheckFileInfoTests
{
    private static WopiCheckFileInfo MinimallyValid() => new()
    {
        BaseFileName = "doc.docx",
        OwnerId = "owner",
        UserId = "user",
        Version = "1",
    };

    [Fact]
    public void Serialization_EmitsWopiWireNames_AndHonoursJsonIgnore()
    {
        // Pins the CheckFileInfo wire contract the same way GetCheckFileInfo produces it
        // (Serialize<object> with default options — WOPI property names are PascalCase).
        var sut = MinimallyValid();
        sut.CloseButtonClosesWindow = true;
        sut.EditAndReplyUrl = new Uri("https://host/edit-and-reply");
#pragma warning disable CS0618 // exercising deprecated members deliberately
        sut.HostName = "host-name";
#pragma warning restore CS0618

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize<object>(sut));
        var root = doc.RootElement;

        Assert.Equal("doc.docx", root.GetProperty("BaseFileName").GetString());
        Assert.Equal("owner", root.GetProperty("OwnerId").GetString());
        Assert.Equal("user", root.GetProperty("UserId").GetString());
        Assert.Equal("1", root.GetProperty("Version").GetString());

        // Members marked [JsonIgnore] (deprecated, unused-future, or deliberately withheld —
        // CloseButtonClosesWindow must not reach M365) stay off the wire even when set.
        Assert.False(root.TryGetProperty("CloseButtonClosesWindow", out _));
        Assert.False(root.TryGetProperty("EditAndReplyUrl", out _));
        Assert.False(root.TryGetProperty("HostName", out _));
    }

    [Fact]
    public void Serialization_SuppressesNullOptionalProperties()
    {
        // WhenWritingNull-conditioned members are absent when unset and present once populated,
        // so WOPI clients never see explicit nulls for optional capabilities.
        var unset = MinimallyValid();
        using (var doc = JsonDocument.Parse(JsonSerializer.Serialize<object>(unset)))
        {
            Assert.False(doc.RootElement.TryGetProperty("DownloadUrl", out _));
        }

        var set = MinimallyValid();
        set.DownloadUrl = new Uri("https://host/download");
        using (var doc = JsonDocument.Parse(JsonSerializer.Serialize<object>(set)))
        {
            Assert.Equal("https://host/download", doc.RootElement.GetProperty("DownloadUrl").GetString());
        }
    }
}
