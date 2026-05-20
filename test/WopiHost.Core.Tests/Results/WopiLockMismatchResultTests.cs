using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Results;

/// <summary>
/// Unit tests for <see cref="WopiLockMismatchResult"/>: the four ctor-arg branches
/// (existingLock null/non-null × reason null/non-null), the 409 status code, the
/// EmptyLockHeaderValue fallback resolution from DI, and the IWopiOutcomeResult
/// marker exposure.
/// </summary>
public class WopiLockMismatchResultTests
{
    [Fact]
    public async Task ExecuteAsync_NoExistingLock_NoReason_WritesEmptyLockHeader_And409()
    {
        var ctx = NewContext();
        var result = new WopiLockMismatchResult();

        await result.ExecuteAsync(ctx);

        Assert.Equal(StatusCodes.Status409Conflict, ctx.Response.StatusCode);
        Assert.Equal(WopiHeaders.EMPTY_LOCK_VALUE, ctx.Response.Headers[WopiHeaders.LOCK].ToString());
        Assert.False(ctx.Response.Headers.ContainsKey(WopiHeaders.LOCK_FAILURE_REASON));
    }

    [Fact]
    public async Task ExecuteAsync_ExistingLock_WritesLockHeader()
    {
        var ctx = NewContext();
        var result = new WopiLockMismatchResult(existingLock: "abc-123");

        await result.ExecuteAsync(ctx);

        Assert.Equal("abc-123", ctx.Response.Headers[WopiHeaders.LOCK].ToString());
    }

    [Fact]
    public async Task ExecuteAsync_WithReason_WritesLockFailureReasonHeader()
    {
        var ctx = NewContext();
        var result = new WopiLockMismatchResult(existingLock: "x", reason: "Lock changed concurrently");

        await result.ExecuteAsync(ctx);

        Assert.Equal("Lock changed concurrently", ctx.Response.Headers[WopiHeaders.LOCK_FAILURE_REASON].ToString());
    }

    [Fact]
    public async Task ExecuteAsync_HonoursWopiHostOptionsEmptyLockHeaderValue()
    {
        // Hosts running under IIS in-process can opt back into the historic single-space
        // workaround by overriding EmptyLockHeaderValue. The result must consult DI to pick
        // the override up rather than baking the spec-compliant default in.
        var ctx = NewContext(o => o.EmptyLockHeaderValue = " ");
        var result = new WopiLockMismatchResult();

        await result.ExecuteAsync(ctx);

        Assert.Equal(" ", ctx.Response.Headers[WopiHeaders.LOCK].ToString());
    }

    [Fact]
    public void Outcome_Is_LockMismatch()
    {
        // IWopiOutcomeResult.Outcome is what the telemetry endpoint filter consumes to
        // distinguish lock mismatches from generic 409 conflicts.
        var result = new WopiLockMismatchResult();
        Assert.Equal(WopiTelemetry.Outcomes.LockMismatch, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_NullHttpContext_Throws()
    {
        var result = new WopiLockMismatchResult();
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.ExecuteAsync(null!));
    }

    private static DefaultHttpContext NewContext(Action<WopiHostOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddOptions<WopiHostOptions>().Configure(o =>
        {
            o.EmptyLockHeaderValue = WopiHeaders.EMPTY_LOCK_VALUE;
            configure?.Invoke(o);
        });
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }
}
