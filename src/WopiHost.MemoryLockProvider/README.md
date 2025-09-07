# WopiHost.MemoryLockProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.MemoryLockProvider.svg)](https://www.nuget.org/packages/WopiHost.MemoryLockProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.MemoryLockProvider.svg)](https://www.nuget.org/packages/WopiHost.MemoryLockProvider)

A .NET library providing an in-memory implementation of WOPI (Web Application Open Platform Interface) file locking. This package implements `IWopiLockProvider` using in-memory storage, making it ideal for single-instance WOPI hosts or development scenarios.

## Features

- **In-Memory Locking**: Fast, in-memory file lock management
- **Thread-Safe Operations**: Concurrent access support
- **Automatic Expiration**: Built-in lock timeout handling
- **Simple Integration**: Easy to use with any WOPI host
- **Development Ready**: Perfect for local development and testing

## Installation

```bash
dotnet add package WopiHost.MemoryLockProvider
```

## Quick Start

### Basic Usage

```csharp
using WopiHost.MemoryLockProvider;
using WopiHost.Core.Extensions;

// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register the memory lock provider
builder.Services.AddScoped<IWopiLockProvider, MemoryLockProvider>();

// Add WOPI
builder.Services.AddWopi();

var app = builder.Build();
app.Run();
```

### With Custom Configuration

```csharp
// Register with custom timeout
builder.Services.AddScoped<IWopiLockProvider>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<MemoryLockProvider>>();
    return new MemoryLockProvider(logger, TimeSpan.FromMinutes(30)); // 30-minute default timeout
});
```

## Hero Scenarios

### 1. Development WOPI Host

Perfect for local development and testing:

```csharp
public class DevelopmentWopiHost
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure for development
        builder.Services.Configure<WopiHostOptions>(options =>
        {
            options.ClientUrl = new Uri("https://localhost:8080");
            options.StorageProviderAssemblyName = "WopiHost.FileSystemProvider";
            options.LockProviderAssemblyName = "WopiHost.MemoryLockProvider";
        });
        
        // Register services
        builder.Services.AddSingleton<InMemoryFileIds>();
        builder.Services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();
        builder.Services.AddScoped<IWopiWritableStorageProvider, WopiFileSystemProvider>();
        builder.Services.AddScoped<IWopiLockProvider, MemoryLockProvider>();
        builder.Services.AddScoped<IWopiSecurityHandler, WopiSecurityHandler>();
        
        // Add WOPI
        builder.Services.AddWopi();
        
        var app = builder.Build();
        
        // Configure pipeline
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        
        app.Run();
    }
}
```

### 2. Single-Instance Production Host

For single-instance production deployments:

```csharp
public class SingleInstanceWopiHost
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure for production
        builder.Services.Configure<WopiHostOptions>(options =>
        {
            options.ClientUrl = new Uri("https://your-wopi-host.com");
            options.StorageProviderAssemblyName = "YourStorageProvider";
            options.LockProviderAssemblyName = "WopiHost.MemoryLockProvider";
        });
        
        // Register services with custom lock timeout
        builder.Services.AddScoped<IWopiLockProvider>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<MemoryLockProvider>>();
            return new MemoryLockProvider(logger, TimeSpan.FromHours(1)); // 1-hour default timeout
        });
        
        // Add other services...
        builder.Services.AddWopi();
        
        var app = builder.Build();
        app.Run();
    }
}
```

### 3. Testing and Integration

Use for testing and integration scenarios:

```csharp
public class WopiHostTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IWopiLockProvider _lockProvider;
    
    public WopiHostTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IWopiLockProvider, MemoryLockProvider>();
        
        _serviceProvider = services.BuildServiceProvider();
        _lockProvider = _serviceProvider.GetRequiredService<IWopiLockProvider>();
    }
    
    [Fact]
    public async Task LockFile_ShouldSucceed()
    {
        // Arrange
        var resourceId = "test-file-123";
        var lockId = "lock-456";
        var timeout = TimeSpan.FromMinutes(5);
        
        // Act
        var result = await _lockProvider.LockAsync(resourceId, lockId, timeout);
        
        // Assert
        Assert.True(result);
        
        // Verify lock exists
        var existingLock = await _lockProvider.GetLockAsync(resourceId);
        Assert.Equal(lockId, existingLock);
    }
    
    [Fact]
    public async Task LockFile_WhenAlreadyLocked_ShouldFail()
    {
        // Arrange
        var resourceId = "test-file-123";
        var lockId1 = "lock-456";
        var lockId2 = "lock-789";
        var timeout = TimeSpan.FromMinutes(5);
        
        // Act
        var firstLock = await _lockProvider.LockAsync(resourceId, lockId1, timeout);
        var secondLock = await _lockProvider.LockAsync(resourceId, lockId2, timeout);
        
        // Assert
        Assert.True(firstLock);
        Assert.False(secondLock);
    }
    
    [Fact]
    public async Task UnlockFile_ShouldSucceed()
    {
        // Arrange
        var resourceId = "test-file-123";
        var lockId = "lock-456";
        var timeout = TimeSpan.FromMinutes(5);
        
        await _lockProvider.LockAsync(resourceId, lockId, timeout);
        
        // Act
        var result = await _lockProvider.UnlockAsync(resourceId, lockId);
        
        // Assert
        Assert.True(result);
        
        // Verify lock is removed
        var existingLock = await _lockProvider.GetLockAsync(resourceId);
        Assert.Null(existingLock);
    }
}
```

## API Reference

### MemoryLockProvider

In-memory implementation of `IWopiLockProvider`.

#### Constructor

```csharp
public MemoryLockProvider(ILogger<MemoryLockProvider> logger, TimeSpan? defaultTimeout = null)
```

**Parameters:**
- `logger`: Logger instance
- `defaultTimeout`: Default lock timeout (optional)

#### Methods

##### LockAsync

```csharp
public Task<bool> LockAsync(string resourceId, string lockId, TimeSpan timeout)
```

Acquires a lock for a resource.

**Parameters:**
- `resourceId`: Resource identifier
- `lockId`: Lock identifier
- `timeout`: Lock timeout duration

**Returns:** True if lock was acquired successfully

##### UnlockAsync

```csharp
public Task<bool> UnlockAsync(string resourceId, string lockId)
```

Releases a lock for a resource.

**Parameters:**
- `resourceId`: Resource identifier
- `lockId`: Lock identifier

**Returns:** True if lock was released successfully

##### GetLockAsync

```csharp
public Task<string?> GetLockAsync(string resourceId)
```

Gets the current lock for a resource.

**Parameters:**
- `resourceId`: Resource identifier

**Returns:** Lock identifier or null if no lock exists

##### RefreshLockAsync

```csharp
public Task<bool> RefreshLockAsync(string resourceId, string lockId, TimeSpan timeout)
```

Refreshes an existing lock.

**Parameters:**
- `resourceId`: Resource identifier
- `lockId`: Lock identifier
- `timeout`: New lock timeout duration

**Returns:** True if lock was refreshed successfully

## Configuration

### Program.cs

```csharp
// Basic registration
builder.Services.AddScoped<IWopiLockProvider, MemoryLockProvider>();

// With custom timeout
builder.Services.AddScoped<IWopiLockProvider>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<MemoryLockProvider>>();
    return new MemoryLockProvider(logger, TimeSpan.FromMinutes(30));
});
```

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "WopiHost.MemoryLockProvider": "Information"
    }
  }
}
```

## Usage Examples

### Basic Lock Operations

```csharp
public class DocumentService
{
    private readonly IWopiLockProvider _lockProvider;
    private readonly ILogger<DocumentService> _logger;
    
    public async Task<bool> EditDocumentAsync(string documentId, string userId)
    {
        var lockId = $"{userId}-{Guid.NewGuid()}";
        var timeout = TimeSpan.FromMinutes(30);
        
        // Try to acquire lock
        var lockAcquired = await _lockProvider.LockAsync(documentId, lockId, timeout);
        
        if (!lockAcquired)
        {
            _logger.LogWarning("Failed to acquire lock for document {DocumentId}", documentId);
            return false;
        }
        
        try
        {
            // Perform document editing operations
            await PerformDocumentEdit(documentId);
            return true;
        }
        finally
        {
            // Always release the lock
            await _lockProvider.UnlockAsync(documentId, lockId);
        }
    }
    
    private async Task PerformDocumentEdit(string documentId)
    {
        // Document editing logic here
        await Task.Delay(1000); // Simulate work
    }
}
```

### Lock Management Service

```csharp
public class LockManagementService
{
    private readonly IWopiLockProvider _lockProvider;
    private readonly ILogger<LockManagementService> _logger;
    
    public async Task<LockInfo?> GetLockInfoAsync(string resourceId)
    {
        var lockId = await _lockProvider.GetLockAsync(resourceId);
        
        if (lockId == null)
        {
            return null;
        }
        
        return new LockInfo
        {
            ResourceId = resourceId,
            LockId = lockId,
            AcquiredAt = DateTime.UtcNow // Note: MemoryLockProvider doesn't track this
        };
    }
    
    public async Task<bool> ForceUnlockAsync(string resourceId)
    {
        var lockId = await _lockProvider.GetLockAsync(resourceId);
        
        if (lockId == null)
        {
            return false;
        }
        
        return await _lockProvider.UnlockAsync(resourceId, lockId);
    }
    
    public async Task<bool> ExtendLockAsync(string resourceId, TimeSpan newTimeout)
    {
        var lockId = await _lockProvider.GetLockAsync(resourceId);
        
        if (lockId == null)
        {
            return false;
        }
        
        return await _lockProvider.RefreshLockAsync(resourceId, lockId, newTimeout);
    }
}

public class LockInfo
{
    public string ResourceId { get; set; } = string.Empty;
    public string LockId { get; set; } = string.Empty;
    public DateTime AcquiredAt { get; set; }
}
```

### Background Lock Cleanup

```csharp
public class LockCleanupService : BackgroundService
{
    private readonly IWopiLockProvider _lockProvider;
    private readonly ILogger<LockCleanupService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // MemoryLockProvider handles expiration automatically
                // This is just for logging and monitoring
                await LogLockStatistics();
                
                // Wait 5 minutes before next cleanup
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during lock cleanup");
            }
        }
    }
    
    private async Task LogLockStatistics()
    {
        // Log lock statistics if needed
        _logger.LogInformation("Lock cleanup completed at {Time}", DateTime.UtcNow);
    }
}
```

## Performance Characteristics

- **Memory Usage**: Locks are stored in memory, so memory usage grows with the number of active locks
- **Performance**: Very fast operations since everything is in-memory
- **Scalability**: Limited to single-instance deployments
- **Persistence**: Locks are lost on application restart

## Limitations

- **Single Instance**: Not suitable for multi-instance deployments
- **No Persistence**: Locks are lost on application restart
- **Memory Growth**: Memory usage increases with the number of active locks
- **No Distributed Locking**: Cannot coordinate locks across multiple instances

## When to Use

### ✅ Good For:
- Local development and testing
- Single-instance production deployments
- Prototyping and proof-of-concept
- Applications with low lock contention
- Scenarios where lock persistence is not critical

### ❌ Not Suitable For:
- Multi-instance deployments
- High-availability scenarios
- Applications requiring lock persistence
- Distributed systems
- High lock contention scenarios

## Dependencies

- `WopiHost.Abstractions`: Core WOPI interfaces
- `Microsoft.Extensions.Logging.Abstractions`: Logging support

## Testing

The package includes comprehensive unit tests:

```csharp
[Test]
public async Task LockProvider_ShouldHandleConcurrentAccess()
{
    var lockProvider = new MemoryLockProvider(Mock.Of<ILogger<MemoryLockProvider>>());
    var resourceId = "test-resource";
    var tasks = new List<Task<bool>>();
    
    // Create multiple concurrent lock attempts
    for (int i = 0; i < 10; i++)
    {
        var lockId = $"lock-{i}";
        tasks.Add(lockProvider.LockAsync(resourceId, lockId, TimeSpan.FromMinutes(1)));
    }
    
    var results = await Task.WhenAll(tasks);
    
    // Only one should succeed
    Assert.Equal(1, results.Count(r => r));
}
```

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.txt) file for details.

## Contributing

Contributions are welcome! Please read our [Contributing Guidelines](../../CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Support

For support and questions:
- Create an issue on [GitHub](https://github.com/petrsvihlik/WopiHost/issues)
- Check the [documentation](https://github.com/petrsvihlik/WopiHost)
- Review the [WOPI specification](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery)
