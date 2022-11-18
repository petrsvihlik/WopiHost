namespace WopiHost.Core.Tests;

public class ExtensionsTests
{
    [Fact]
    public void ToUnixTimestampTest()
    {
        // Arrange
        long ticks = 1664582400;
        DateTime dateTime = new(2022, 10, 1);

        // Act

        long actual = dateTime.ToUnixTimestamp();

        // Assert
        Assert.Equal(ticks, actual);
    }
}
