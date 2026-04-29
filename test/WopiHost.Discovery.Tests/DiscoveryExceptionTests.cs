namespace WopiHost.Discovery.Tests;

public class DiscoveryExceptionTests
{
    [Fact]
    public void DefaultConstructor_HasNonNullMessage()
    {
        var ex = new DiscoveryException();

        Assert.NotNull(ex.Message);
    }

    [Fact]
    public void MessageConstructor_StoresMessage()
    {
        var ex = new DiscoveryException("boom");

        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void InnerExceptionConstructor_StoresMessageAndInner()
    {
        var inner = new InvalidOperationException("inner");

        var ex = new DiscoveryException("outer", inner);

        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
