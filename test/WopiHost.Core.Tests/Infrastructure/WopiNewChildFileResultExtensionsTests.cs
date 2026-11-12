using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Tests for the WOPI-spec response-mapping in <see cref="WopiNewChildFileResultExtensions.ToFailureResult"/>.
/// Maps each failed <see cref="WopiNewChildFileResult"/> case to its response plus the
/// spec-mandated response-header side effects.
/// </summary>
public class WopiNewChildFileResultExtensionsTests
{
    private readonly HttpResponse _response = new DefaultHttpContext().Response;

    [Fact]
    public void Success_Throws()
    {
        var result = new WopiNewChildFileResult.Success(Mock.Of<IWopiWritableFile>());

        Assert.Throws<ArgumentException>(() => result.ToFailureResult(_response));
    }

    [Fact]
    public void BadRequest_ReturnsBadRequest_WithoutHeaders()
    {
        var result = new WopiNewChildFileResult.BadRequest();

        var actionResult = result.ToFailureResult(_response).Result;

        Assert.IsType<BadRequest>(actionResult);
        Assert.Empty(_response.Headers);
    }

    [Fact]
    public void Conflict_WritesValidRelativeTargetHeader_AndReturnsConflict()
    {
        var result = new WopiNewChildFileResult.Conflict("Report (1).docx");

        var actionResult = result.ToFailureResult(_response).Result;

        Assert.IsType<Conflict>(actionResult);
        Assert.True(_response.Headers.TryGetValue(WopiHeaders.ValidRelativeTarget, out var target));
        // Header value is UTF-7 encoded per the WOPI spec; every character of this suggestion
        // sits in UTF-7's direct set, so the wire value matches the decoded name byte-for-byte.
        // Pinning the exact name matters — the header exists for the client's auto-retry, so
        // writing the wrong name (e.g. the conflicting original) must fail here.
        Assert.Equal("Report (1).docx", target.ToString());
    }

    [Fact]
    public async Task Locked_ReturnsWopiLockMismatchResult_WithExistingLockHeader()
    {
        var result = new WopiNewChildFileResult.Locked("active-lock-id");

        var actionResult = result.ToFailureResult(_response).Result;

        var lockMismatch = Assert.IsType<WopiLockMismatchResult>(actionResult);
        // WopiLockMismatchResult writes the lock header on ExecuteAsync, so trigger the side
        // effect explicitly before asserting on response headers.
        await lockMismatch.ExecuteAsync(_response.HttpContext);
        Assert.Equal("active-lock-id", _response.Headers[WopiHeaders.Lock].ToString());
    }

    [Fact]
    public void InternalError_Returns500()
    {
        var result = new WopiNewChildFileResult.InternalError();

        var actionResult = result.ToFailureResult(_response).Result;

        var status = Assert.IsType<IStatusCodeHttpResult>(actionResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public void NullResult_Throws()
    {
        WopiNewChildFileResult result = null!;

        Assert.Throws<ArgumentNullException>(() => result.ToFailureResult(_response));
    }

    [Fact]
    public void NullResponse_Throws()
    {
        var result = new WopiNewChildFileResult.BadRequest();

        Assert.Throws<ArgumentNullException>(() => result.ToFailureResult(null!));
    }
}
