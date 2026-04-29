using Microsoft.AspNetCore.Http;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Results;

public class InvalidContainerNameResultTests
{
    [Fact]
    public void Constructor_WithReason_SetsReasonAndHeader()
    {
        var ctx = new DefaultHttpContext();

        var sut = new InvalidContainerNameResult(ctx.Response, "name too long");

        Assert.Equal("name too long", sut.Reason);
        Assert.Equal("name too long", ctx.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME]);
    }

    [Fact]
    public void Constructor_WithoutReason_LeavesReasonNull()
    {
        var ctx = new DefaultHttpContext();

        var sut = new InvalidContainerNameResult(ctx.Response);

        Assert.Null(sut.Reason);
        Assert.False(ctx.Response.Headers.ContainsKey(WopiHeaders.INVALID_CONTAINER_NAME));
    }

    [Fact]
    public void Constructor_WithEmptyReason_LeavesReasonNull()
    {
        var ctx = new DefaultHttpContext();

        var sut = new InvalidContainerNameResult(ctx.Response, "");

        Assert.Null(sut.Reason);
        Assert.False(ctx.Response.Headers.ContainsKey(WopiHeaders.INVALID_CONTAINER_NAME));
    }
}
