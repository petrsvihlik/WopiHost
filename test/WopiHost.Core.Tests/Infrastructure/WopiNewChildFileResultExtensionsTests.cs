using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Tests for the WOPI-spec response-mapping in <see cref="WopiNewChildFileResultExtensions.ToErrorResult"/>.
/// Translates the negotiator's <see cref="WopiNewChildFileResult"/> into the appropriate
/// <see cref="IResult"/> plus the spec-mandated response-header side effects.
/// </summary>
public class WopiNewChildFileResultExtensionsTests
{
    private readonly HttpResponse _response = new DefaultHttpContext().Response;

    [Fact]
    public void Success_ReturnsNull()
    {
        // Null is the explicit "no error result — caller should proceed with result.File" signal.
        var result = WopiNewChildFileResult.Success(Mock.Of<IWopiWritableFile>());

        Assert.Null(result.ToErrorResult(_response));
    }

    [Fact]
    public void BadRequest_ReturnsBadRequest_WithoutHeaders()
    {
        var result = WopiNewChildFileResult.BadRequest();

        var actionResult = result.ToErrorResult(_response);

        Assert.IsType<BadRequest>(actionResult);
        Assert.Empty(_response.Headers);
    }

    [Fact]
    public void Conflict_WritesValidRelativeTargetHeader_AndReturnsConflict()
    {
        var result = WopiNewChildFileResult.Conflict("Report (1).docx");

        var actionResult = result.ToErrorResult(_response);

        Assert.IsType<Conflict>(actionResult);
        Assert.True(_response.Headers.ContainsKey(WopiHeaders.ValidRelativeTarget));
        // Header value is UTF-7 encoded per the WOPI spec — the round-trip is asserted in
        // UtfStringTests; this only checks the header was set with non-empty content.
        Assert.NotEmpty(_response.Headers[WopiHeaders.ValidRelativeTarget].ToString());
    }

    [Fact]
    public async Task Locked_ReturnsWopiLockMismatchResult_WithExistingLockHeader()
    {
        var result = WopiNewChildFileResult.Locked("active-lock-id");

        var actionResult = result.ToErrorResult(_response);

        var lockMismatch = Assert.IsType<WopiLockMismatchResult>(actionResult);
        // WopiLockMismatchResult writes the lock header on ExecuteAsync, so trigger the side
        // effect explicitly before asserting on response headers.
        await lockMismatch.ExecuteAsync(_response.HttpContext);
        Assert.Equal("active-lock-id", _response.Headers[WopiHeaders.Lock].ToString());
    }

    [Fact]
    public void InternalError_Returns500()
    {
        var result = WopiNewChildFileResult.InternalError();

        var actionResult = result.ToErrorResult(_response);

        var status = Assert.IsType<IStatusCodeHttpResult>(actionResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public void UnknownOutcome_Throws()
    {
        // The switch is exhaustive at compile time but uses a `default: throw` arm as a guard
        // for future enum additions or hand-crafted instances. Construct an out-of-range
        // outcome via the property init and verify the throw fires.
        var result = new WopiNewChildFileResult { Outcome = (WopiNewChildFileOutcome)999 };

        var ex = Assert.Throws<InvalidOperationException>(() => result.ToErrorResult(_response));
        Assert.Contains("999", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullResult_Throws()
    {
        WopiNewChildFileResult result = null!;

        Assert.Throws<ArgumentNullException>(() => result.ToErrorResult(_response));
    }

    [Fact]
    public void NullResponse_Throws()
    {
        var result = WopiNewChildFileResult.BadRequest();

        Assert.Throws<ArgumentNullException>(() => result.ToErrorResult(null!));
    }
}
