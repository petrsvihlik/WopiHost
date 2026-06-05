using Xunit;

namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Conformance test suite every <see cref="IWopiLockProvider"/> implementation must satisfy.
/// Derive a concrete sealed subclass in each provider's test project and override
/// <see cref="Factory"/> to return an <see cref="ILockProviderTestFactory"/> that produces a
/// fresh SUT per test. xUnit's test discovery picks up tests on base classes automatically,
/// so no additional plumbing is needed on the derived class beyond the property override.
/// </summary>
/// <remarks>
/// <para>
/// The suite intentionally drives behavior through the public interface only — no reflection
/// into private state, no impl-specific seeding. Expiry is tested by advancing a
/// <see cref="ControllableTimeProvider"/>; the lock-comparer seam is tested by injecting
/// <see cref="JsonShapedWopiLockComparer"/>. Tests that depend on impl-specific shape
/// (e.g. seeding a stale blob by writing directly to Azure metadata; reflecting into
/// MemoryLockProvider's static dictionary) belong in the provider's own test project, not here.
/// </para>
/// <para>
/// File ids are GUID-suffixed per test so accidental cross-test interference (relevant for
/// providers that share state across instances) doesn't manifest as flakes.
/// </para>
/// </remarks>
public abstract class LockProviderConformanceTests
{
    /// <summary>
    /// Override to supply a factory that builds a fresh SUT per test for this provider.
    /// </summary>
    protected abstract ILockProviderTestFactory Factory { get; }

    private Task<IWopiLockProvider> CreateSutAsync(TimeProvider? clock = null, IWopiLockComparer? comparer = null)
        => Factory.CreateAsync(clock ?? TimeProvider.System, comparer);

    [Fact]
    public async Task AddLockAsync_NoExistingLock_Succeeds()
    {
        var sut = await CreateSutAsync();
        var fileId = $"add-{Guid.NewGuid()}";

        var info = await sut.AddLockAsync(fileId, "lock-A");

        Assert.NotNull(info);
        Assert.Equal(fileId, info.FileId);
        Assert.Equal("lock-A", info.LockId);
        Assert.False(info.IsExpiredAt(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task AddLockAsync_WhenAlreadyLocked_ReturnsNull()
    {
        var sut = await CreateSutAsync();
        var fileId = $"dup-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "first");

        var second = await sut.AddLockAsync(fileId, "second");

        Assert.Null(second);
    }

    [Fact]
    public async Task AddLockAsync_AfterPreviousLockRemoved_Succeeds()
    {
        var sut = await CreateSutAsync();
        var fileId = $"re-add-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "lock-A");
        await sut.RemoveLockAsync(fileId);

        var second = await sut.AddLockAsync(fileId, "lock-B");

        Assert.NotNull(second);
        Assert.Equal("lock-B", second.LockId);
    }

    [Fact]
    public async Task GetLockAsync_ReturnsLock_WhenPresent()
    {
        var sut = await CreateSutAsync();
        var fileId = $"get-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "lock-A");

        var info = await sut.GetLockAsync(fileId);

        Assert.NotNull(info);
        Assert.Equal(fileId, info.FileId);
        Assert.Equal("lock-A", info.LockId);
    }

    [Fact]
    public async Task GetLockAsync_ReturnsNull_WhenAbsent()
    {
        var sut = await CreateSutAsync();

        var info = await sut.GetLockAsync($"missing-{Guid.NewGuid()}");

        Assert.Null(info);
    }

    [Fact]
    public async Task GetLockAsync_AfterClockAdvancesPastExpiry_ReturnsNull()
    {
        // Drives expiry through the public TimeProvider seam — no reaching into impl state to
        // backdate a record. Both MemoryLockProvider and WopiAzureLockProvider read their clock
        // through the injected TimeProvider, so advancing this clock past the 30-minute window
        // is the same as wall time advancing in production.
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        var sut = await CreateSutAsync(clock);
        var fileId = $"expire-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "lock-A");

        // One minute before expiry: still observable.
        clock.Now = clock.Now.AddMinutes(WopiLockInfo.ExpirationMinutes - 1);
        Assert.NotNull(await sut.GetLockAsync(fileId));

        // Past expiry: evicted.
        clock.Now = clock.Now.AddMinutes(2);
        Assert.Null(await sut.GetLockAsync(fileId));
    }

    [Fact]
    public async Task RefreshLockAsync_MatchingExpectedLock_UpdatesTimestamp_PreservingLockId()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        var sut = await CreateSutAsync(clock);
        var fileId = $"refresh-{Guid.NewGuid()}";
        var original = await sut.AddLockAsync(fileId, "lock-A");
        Assert.NotNull(original);

        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await sut.RefreshLockAsync(fileId, expectedExistingLockId: "lock-A");

        Assert.True(refreshed);
        var info = await sut.GetLockAsync(fileId);
        Assert.NotNull(info);
        // RefreshLockAsync bumps the timestamp; for an id-swap semantic, callers use TryUnlockAndRelockAsync.
        Assert.Equal("lock-A", info.LockId);
        Assert.True(info.DateCreated > original.DateCreated);
    }

    [Fact]
    public async Task RefreshLockAsync_MismatchedExpectedLock_ReturnsFalseAndDoesNotMutate()
    {
        // Spec: RefreshLock honours the caller's lock id only when it matches the stored id.
        // The provider performs an atomic compare-and-refresh; a stale caller observes false
        // here and the stored id / timestamp are untouched.
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        var sut = await CreateSutAsync(clock);
        var fileId = $"refresh-mismatch-{Guid.NewGuid()}";
        var original = await sut.AddLockAsync(fileId, "current-lock");
        Assert.NotNull(original);

        clock.Now = clock.Now.AddMinutes(1);
        var refreshed = await sut.RefreshLockAsync(fileId, expectedExistingLockId: "stale-cached-lock");

        Assert.False(refreshed);
        var info = await sut.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("current-lock", info.LockId);
        // Timestamp must not have moved on a mismatch — otherwise a stale caller could keep a
        // lock alive past its 30-minute window without ever proving ownership.
        Assert.Equal(original.DateCreated, info.DateCreated);
    }

    [Fact]
    public async Task RefreshLockAsync_NoExistingLock_ReturnsFalse()
    {
        var sut = await CreateSutAsync();

        var result = await sut.RefreshLockAsync($"missing-{Guid.NewGuid()}", expectedExistingLockId: "anything");

        Assert.False(result);
    }

    [Fact]
    public async Task RefreshLockAsync_ConcurrentSwapBetweenObservationAndCAS_DoesNotRefresh()
    {
        // Same race as TryUnlockAndRelockAsync's CAS test, but for Refresh: caller A reads the
        // lock as "old-lock", is about to refresh. Before A's refresh lands, caller B swaps the
        // lock id to "B-new" (legit UnlockAndRelock). A's refresh must NOT bump the timestamp on
        // "B-new" — the spec is that A's stale id no longer matches what's stored.
        var sut = await CreateSutAsync();
        var fileId = $"refresh-race-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "old-lock");

        // B swaps first.
        var bSwapped = await sut.TryUnlockAndRelockAsync(fileId, "B-new", expectedExistingLockId: "old-lock");
        Assert.True(bSwapped);

        // A still believes the lock is "old-lock" — refresh must fail.
        var aRefreshed = await sut.RefreshLockAsync(fileId, expectedExistingLockId: "old-lock");
        Assert.False(aRefreshed);

        var info = await sut.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("B-new", info.LockId);
    }

    [Fact]
    public async Task RefreshLockAsync_AdvancingClockPastExpiry_ResetsClock()
    {
        // RefreshLockAsync must write back the *new* clock reading, not the original DateCreated,
        // so a long-running session that keeps refreshing never expires.
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        var sut = await CreateSutAsync(clock);
        var fileId = $"refresh-extend-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "lock-A");

        // Almost-expired, then refresh with the matching lock id.
        clock.Now = clock.Now.AddMinutes(WopiLockInfo.ExpirationMinutes - 1);
        Assert.True(await sut.RefreshLockAsync(fileId, expectedExistingLockId: "lock-A"));

        // Another almost-expiry-window later: still alive because the refresh moved DateCreated.
        clock.Now = clock.Now.AddMinutes(WopiLockInfo.ExpirationMinutes - 1);
        Assert.NotNull(await sut.GetLockAsync(fileId));
    }

    [Fact]
    public async Task RefreshLockAsync_AfterClockAdvancesPastExpiry_ReturnsFalseAndEvicts()
    {
        // Conformance flip-side of RefreshLockAsync_AdvancingClockPastExpiry_ResetsClock: a
        // caller that misses the refresh window must see false (and providers should evict the
        // stale record). Important even when the lock-id still matches — expiry shouldn't be
        // bypassable by a caller who happens to know the right id.
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        var sut = await CreateSutAsync(clock);
        var fileId = $"refresh-past-expiry-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "lock-A");

        clock.Now = clock.Now.AddMinutes(WopiLockInfo.ExpirationMinutes + 1);

        Assert.False(await sut.RefreshLockAsync(fileId, expectedExistingLockId: "lock-A"));
        Assert.Null(await sut.GetLockAsync(fileId));
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_AfterClockAdvancesPastExpiry_ReturnsFalse()
    {
        // Same flip-side for the swap path: a stale UnlockAndRelock attempt that arrives past
        // the WOPI 30-minute window must NOT succeed, even with the right expected lock id.
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        var sut = await CreateSutAsync(clock);
        var fileId = $"swap-past-expiry-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "old-lock");

        clock.Now = clock.Now.AddMinutes(WopiLockInfo.ExpirationMinutes + 1);

        Assert.False(await sut.TryUnlockAndRelockAsync(fileId, "new-lock", expectedExistingLockId: "old-lock"));
        Assert.Null(await sut.GetLockAsync(fileId));
    }

    [Fact]
    public async Task RemoveLockAsync_ClearsLock()
    {
        var sut = await CreateSutAsync();
        var fileId = $"remove-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "lock-A");

        var removed = await sut.RemoveLockAsync(fileId);

        Assert.True(removed);
        Assert.Null(await sut.GetLockAsync(fileId));
    }

    [Fact]
    public async Task RemoveLockAsync_NoLock_ReturnsFalse()
    {
        var sut = await CreateSutAsync();

        var result = await sut.RemoveLockAsync($"never-locked-{Guid.NewGuid()}");

        Assert.False(result);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_MatchingExpectedLock_SwapsAtomically()
    {
        var sut = await CreateSutAsync();
        var fileId = $"swap-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "old-lock");

        var swapped = await sut.TryUnlockAndRelockAsync(fileId, "new-lock", expectedExistingLockId: "old-lock");

        Assert.True(swapped);
        var info = await sut.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("new-lock", info.LockId);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_MismatchedExpectedLock_ReturnsFalseAndDoesNotMutate()
    {
        var sut = await CreateSutAsync();
        var fileId = $"swap-mismatch-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "current-lock");

        var swapped = await sut.TryUnlockAndRelockAsync(fileId, "new-lock", expectedExistingLockId: "stale-cached-lock");

        Assert.False(swapped);
        var info = await sut.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("current-lock", info.LockId);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_NoExistingLock_ReturnsFalse()
    {
        var sut = await CreateSutAsync();
        var fileId = $"swap-missing-{Guid.NewGuid()}";

        var swapped = await sut.TryUnlockAndRelockAsync(fileId, "new-lock", expectedExistingLockId: "anything");

        Assert.False(swapped);
        Assert.Null(await sut.GetLockAsync(fileId));
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_ConcurrentSwapBetweenObservationAndCAS_DoesNotStealLock()
    {
        // The race: caller A reads the lock as "old-lock", is about to swap to "A-new". Before
        // A's CAS lands, caller B successfully swaps "old-lock" → "B-new". Without atomic CAS,
        // A's swap would silently overwrite B's lock. Both providers must defend this.
        //   - MemoryLockProvider: ConcurrentDictionary.TryUpdate with comparand.
        //   - WopiAzureLockProvider: ETag-conditional metadata update (IfMatch=etag, 412 on race).
        var sut = await CreateSutAsync();
        var fileId = $"race-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "old-lock");

        // Simulate caller B winning the race first.
        var bSwapped = await sut.TryUnlockAndRelockAsync(fileId, "B-new", expectedExistingLockId: "old-lock");
        Assert.True(bSwapped);

        // Caller A still thinks the lock is "old-lock" — its CAS must fail.
        var aSwapped = await sut.TryUnlockAndRelockAsync(fileId, "A-new", expectedExistingLockId: "old-lock");
        Assert.False(aSwapped);

        var info = await sut.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("B-new", info.LockId);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_JsonShapedLockWithExtraProperty_FailsUnderDefaultStrictCompare()
    {
        // The default IWopiLockComparer is OrdinalWopiLockComparer (byte-exact). A WOPI client
        // that re-serializes its JSON lock id with an extra property must fail the swap under
        // strict compare. If a real M365 / OOS deployment flakes on this in production, the
        // remedy is to swap in JsonShapedWopiLockComparer (or a custom comparer tailored to the
        // observed mutation), NOT to relax this test.
        var sut = await CreateSutAsync();
        var fileId = $"json-strict-{Guid.NewGuid()}";
        const string clientLock = """{"S":"abc-123","F":4}""";
        const string clientLockMutated = """{"S":"abc-123","F":4,"V":1}""";
        await sut.AddLockAsync(fileId, clientLock);

        var swapped = await sut.TryUnlockAndRelockAsync(fileId, "next-lock", expectedExistingLockId: clientLockMutated);

        Assert.False(swapped);
        var info = await sut.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal(clientLock, info.LockId);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_JsonShapedLockWithExtraProperty_SwapsUnderJsonShapedComparer()
    {
        // Same scenario, tolerant comparer plugged in: the swap succeeds because the OOS-style
        // mutation only added a property — the underlying S-field identity hasn't changed.
        var sut = await CreateSutAsync(comparer: new JsonShapedWopiLockComparer());
        var fileId = $"json-tolerant-{Guid.NewGuid()}";
        const string clientLock = """{"S":"abc-123","F":4}""";
        const string clientLockMutated = """{"S":"abc-123","F":4,"V":1}""";
        await sut.AddLockAsync(fileId, clientLock);

        var swapped = await sut.TryUnlockAndRelockAsync(fileId, "next-lock", expectedExistingLockId: clientLockMutated);

        Assert.True(swapped);
        var info = await sut.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("next-lock", info.LockId);
    }

    public static TheoryData<string, string> ExoticLockIds() => new()
    {
        // Escaped quotes inside the JSON payload — Office Online Server has shipped lock ids
        // shaped like {"S":"abc \"quoted\" def"} in the wild. Round-trip must preserve the
        // backslash + quote pair verbatim.
        { "lock-with-escaped-quotes", """{"S":"abc \"quoted\" def","F":4}""" },
        // JSON null values inside the payload — also seen in OOS. The provider stores the byte
        // sequence; the comparer never reaches a string-null because the lock id itself is the
        // serialised JSON.
        { "lock-with-null-value", """{"S":"abc","E":null,"F":4}""" },
        // Concrete OOS lock id shape (the F-field carries spec-defined flags; S is the session
        // hash). Pinning the literal shape catches regressions where a provider accidentally
        // trims/normalises before storing.
        { "lock-shape-oos", """{"S":"AB12CD34","F":0}""" },
        // Special ASCII characters that interact badly with naive string handling — equals
        // sign + colon + semicolon are header-syntax-relevant and tab is invisible; all
        // permitted by the WOPI spec (ASCII 32-126 + tab in practice).
        { "lock-with-special-ascii", "key=value;flag:on" },
        // WOPI permits ASCII 32-126; embed the full punctuation cluster as a stress test to
        // catch providers that escape/normalise specific characters.
        { "lock-with-punctuation", "lock-!@#$%^&*()_+-=[]{}|;':\",./<>?" },
    };

    [Theory]
    [MemberData(nameof(ExoticLockIds))]
    public async Task AddLockAsync_PreservesExoticLockIdBytes_OnGet(string label, string lockId)
    {
        // The provider stores the lock id; GetLockAsync must surface the IDENTICAL byte sequence
        // back. Skipping this round-trip cover means a provider could trim, normalise, or lose
        // characters silently and only the JsonShaped tests in this suite would catch it.
        _ = label;
        var sut = await CreateSutAsync();
        var fileId = $"exotic-roundtrip-{Guid.NewGuid()}";

        var added = await sut.AddLockAsync(fileId, lockId);
        Assert.NotNull(added);
        Assert.Equal(lockId, added.LockId);

        var fetched = await sut.GetLockAsync(fileId);
        Assert.NotNull(fetched);
        Assert.Equal(lockId, fetched.LockId);
    }

    [Theory]
    [MemberData(nameof(ExoticLockIds))]
    public async Task TryUnlockAndRelockAsync_PreservesExoticLockIdBytes_OnSwap(string label, string lockId)
    {
        // Same round-trip pin for the CAS swap path — a provider that stores the new lock id via
        // a separate code path (e.g. Azure metadata vs Lease) might handle exotic bytes
        // differently on Add vs Swap.
        _ = label;
        var sut = await CreateSutAsync();
        var fileId = $"exotic-swap-{Guid.NewGuid()}";
        await sut.AddLockAsync(fileId, "initial-lock");

        var swapped = await sut.TryUnlockAndRelockAsync(fileId, lockId, expectedExistingLockId: "initial-lock");
        Assert.True(swapped);

        var fetched = await sut.GetLockAsync(fileId);
        Assert.NotNull(fetched);
        Assert.Equal(lockId, fetched.LockId);
    }
}
