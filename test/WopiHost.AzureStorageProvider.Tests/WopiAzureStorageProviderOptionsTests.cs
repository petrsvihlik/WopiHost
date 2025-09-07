using Microsoft.Extensions.Configuration;

namespace WopiHost.AzureStorageProvider.Tests;

public class WopiAzureStorageProviderOptionsTests
{
    [Fact]
    public void Options_WithConnectionString_ShouldBeValid()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WopiHost:StorageOptions:ConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=testkey",
                ["WopiHost:StorageOptions:ContainerName"] = "test-container"
            })
            .Build();

        // Act
        var options = configuration.GetSection("WopiHost:StorageOptions")
            .Get<WopiAzureStorageProviderOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("DefaultEndpointsProtocol=https;AccountName=test;AccountKey=testkey", options.ConnectionString);
        Assert.Equal("test-container", options.ContainerName);
        Assert.False(options.UseManagedIdentity);
        Assert.True(options.CreateContainerIfNotExists);
        Assert.Equal(250, options.FileNameMaxLength);
    }

    [Fact]
    public void Options_WithManagedIdentity_ShouldBeValid()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WopiHost:StorageOptions:AccountName"] = "test-account",
                ["WopiHost:StorageOptions:ContainerName"] = "test-container",
                ["WopiHost:StorageOptions:UseManagedIdentity"] = "true"
            })
            .Build();

        // Act
        var options = configuration.GetSection("WopiHost:StorageOptions")
            .Get<WopiAzureStorageProviderOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("test-account", options.AccountName);
        Assert.Equal("test-container", options.ContainerName);
        Assert.True(options.UseManagedIdentity);
    }

    [Fact]
    public void Options_WithAccountKey_ShouldBeValid()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WopiHost:StorageOptions:AccountName"] = "test-account",
                ["WopiHost:StorageOptions:AccountKey"] = "test-key",
                ["WopiHost:StorageOptions:ContainerName"] = "test-container"
            })
            .Build();

        // Act
        var options = configuration.GetSection("WopiHost:StorageOptions")
            .Get<WopiAzureStorageProviderOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("test-account", options.AccountName);
        Assert.Equal("test-key", options.AccountKey);
        Assert.Equal("test-container", options.ContainerName);
        Assert.False(options.UseManagedIdentity);
    }
}
