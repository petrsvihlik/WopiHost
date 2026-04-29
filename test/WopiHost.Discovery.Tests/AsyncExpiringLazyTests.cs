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
