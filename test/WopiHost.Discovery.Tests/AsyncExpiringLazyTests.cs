namespace WopiHost.Discovery.Tests;

public class AsyncExpiringLazyTests
{
    private static AsyncExpiringLazy<string> CreateSut(
        Func<TemporaryValue<string>, Task<TemporaryValue<string>>> provider)
        => new(provider);

    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AsyncExpiringLazy<string>(valueProvider: null!));
    }

    [Fact]
    public async Task IsValueCreated_BeforeFirstValue_ReturnsFalse()
    {
        var sut = CreateSut(_ => Task.FromResult(new TemporaryValue<string>
        {
            Result = "x",
            ValidUntil = DateTimeOffset.UtcNow.AddHours(1),
        }));

        Assert.False(await sut.IsValueCreated());
    }

    [Fact]
    public async Task IsValueCreated_AfterFirstValue_ReturnsTrue()
    {
        var sut = CreateSut(_ => Task.FromResult(new TemporaryValue<string>
        {
            Result = "x",
            ValidUntil = DateTimeOffset.UtcNow.AddHours(1),
        }));

        await sut.Value();

        Assert.True(await sut.IsValueCreated());
    }

    [Fact]
    public async Task Value_FirstCall_InvokesProvider()
    {
        var calls = 0;
        var sut = CreateSut(_ =>
        {
            calls++;
            return Task.FromResult(new TemporaryValue<string>
            {
                Result = "hello",
                ValidUntil = DateTimeOffset.UtcNow.AddHours(1),
            });
        });

        var result = await sut.Value();

        Assert.Equal("hello", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Value_SecondCallWithinValidity_ReturnsCached()
    {
        var calls = 0;
        var sut = CreateSut(_ =>
        {
            calls++;
            return Task.FromResult(new TemporaryValue<string>
            {
                Result = "hello",
                ValidUntil = DateTimeOffset.UtcNow.AddHours(1),
            });
        });

        await sut.Value();
        await sut.Value();

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Value_AfterExpiry_RefetchesValue()
    {
        var calls = 0;
        var sut = CreateSut(_ =>
        {
            calls++;
            return Task.FromResult(new TemporaryValue<string>
            {
                Result = $"call-{calls}",
                ValidUntil = DateTimeOffset.UtcNow.AddMilliseconds(-1),
            });
        });

        await sut.Value();
        var second = await sut.Value();

        Assert.Equal(2, calls);
        Assert.Equal("call-2", second);
    }

    [Fact]
    public async Task Value_ConcurrentFirstTimeCallers_InvokeProviderExactlyOnce()
    {
        // Regression test for #303: previously Value() released the lock between
        // the cache-miss check and the fetch, so N concurrent first-time callers
        // each fanned out and ran the (network-bound) provider.
        var calls = 0;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sut = CreateSut(async _ =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return new TemporaryValue<string>
            {
                Result = "hello",
                ValidUntil = DateTimeOffset.UtcNow.AddHours(1),
            };
        });

        var callers = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => sut.Value()))
            .ToArray();

        // Give all callers a chance to queue up on the lock before releasing
        // the provider. Without this delay the first caller can complete
        // before the others race for the lock.
        await Task.Delay(100);

        gate.SetResult(true);

        var results = await Task.WhenAll(callers);

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Equal("hello", r));
    }

    [Fact]
    public async Task Value_TwoInstances_DoNotShareLock()
    {
        // Regression test: previously the lock was static, so two instances
        // of AsyncExpiringLazy<string> serialized through one semaphore even
        // though they cache independent values.
        var gateA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateB = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sutA = CreateSut(async _ =>
        {
            await gateA.Task;
            return new TemporaryValue<string>
            {
                Result = "A",
                ValidUntil = DateTimeOffset.UtcNow.AddHours(1),
            };
        });
        var sutB = CreateSut(async _ =>
        {
            await gateB.Task;
            return new TemporaryValue<string>
            {
                Result = "B",
                ValidUntil = DateTimeOffset.UtcNow.AddHours(1),
            };
        });

        var taskA = Task.Run(() => sutA.Value());
        var taskB = Task.Run(() => sutB.Value());

        // Release B first. With a shared static lock, B would block until A
        // finishes. With instance-scoped locks, B completes independently.
        gateB.SetResult(true);
        Assert.Equal("B", await taskB.WaitAsync(TimeSpan.FromSeconds(5)));

        gateA.SetResult(true);
        Assert.Equal("A", await taskA.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Invalidate_ClearsCachedValue()
    {
        var calls = 0;
        var sut = CreateSut(_ =>
        {
            calls++;
            return Task.FromResult(new TemporaryValue<string>
            {
                Result = "hello",
                ValidUntil = DateTimeOffset.UtcNow.AddHours(1),
            });
        });

        await sut.Value();
        await sut.Invalidate();

        Assert.False(await sut.IsValueCreated());

        await sut.Value();
        Assert.Equal(2, calls);
    }
}
