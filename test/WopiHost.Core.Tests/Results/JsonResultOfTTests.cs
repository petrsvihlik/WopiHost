using System.Text.Json;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Results;

public class JsonResultOfTTests
{
    private sealed record Payload(string Name);

    [Fact]
    public void Constructor_WithValue_ExposesData()
    {
        var payload = new Payload("hello");

        var sut = new JsonResult<Payload>(payload);

        Assert.Same(payload, sut.Data);
        Assert.Same(payload, sut.Value);
    }

    [Fact]
    public void Constructor_WithSerializerSettings_ExposesData()
    {
        var payload = new Payload("hello");
        var settings = new JsonSerializerOptions { WriteIndented = true };

        var sut = new JsonResult<Payload>(payload, settings);

        Assert.Same(payload, sut.Data);
        Assert.Same(settings, sut.SerializerSettings);
    }
}
