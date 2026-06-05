using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using FakeItEasy;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.Cobalt.Tests;

/// <summary>
/// Lifecycle tests for <see cref="CobaltProcessor"/> — construction, disposal, and
/// the cancellation/argument-validation contract on <see cref="CobaltProcessor.ProcessCobalt"/>.
/// A full Cobalt request batch is not driven through ProcessCobalt here: the protocol is
/// binary and exercised end-to-end by the validator + sample apps. Unit tests cover the
/// seams around it.
/// </summary>
public class CobaltProcessorTests
{
    [Fact]
    public void Ctor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CobaltProcessor(null!, NullLogger<CoauthoringSessionTracker>.Instance));
    }

    [Fact]
    public void Ctor_NullTrackerLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CobaltProcessor(NullLogger<CobaltProcessor>.Instance, null!));
    }

    [Fact]
    public async Task ProcessCobalt_NullFile_ThrowsArgumentNullException()
    {
        using var processor = new CobaltProcessor(NullLogger<CobaltProcessor>.Instance, NullLogger<CoauthoringSessionTracker>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => processor.ProcessCobalt(null!, new ClaimsPrincipal(), [], CancellationToken.None));
    }

    [Fact]
    public async Task ProcessCobalt_NullContent_ThrowsArgumentNullException()
    {
        using var processor = new CobaltProcessor(NullLogger<CobaltProcessor>.Instance, NullLogger<CoauthoringSessionTracker>.Instance);
        var file = A.Fake<IWopiWritableFile>();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => processor.ProcessCobalt(file, new ClaimsPrincipal(), null!, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessCobalt_AfterDispose_Throws()
    {
        var processor = new CobaltProcessor(NullLogger<CobaltProcessor>.Instance, NullLogger<CoauthoringSessionTracker>.Instance);
        processor.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => processor.ProcessCobalt(A.Fake<IWopiWritableFile>(), new ClaimsPrincipal(), [], CancellationToken.None));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var processor = new CobaltProcessor(NullLogger<CobaltProcessor>.Instance, NullLogger<CoauthoringSessionTracker>.Instance);
        processor.Dispose();
        processor.Dispose();   // second call must not throw
    }

    [Fact]
    public void SessionIdleTimeout_MatchesDocumentedValue()
    {
        // Pin the documented timeout — sessions live for 60 minutes of inactivity, then the
        // periodic eviction sweeper drops them. Production hosts that want a different
        // window need to fork; this test guards against an accidental change.
        Assert.Equal(TimeSpan.FromMinutes(60), CobaltProcessor.SessionIdleTimeout);
    }
}
