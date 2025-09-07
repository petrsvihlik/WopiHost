# WopiHost.Cobalt

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Cobalt.svg)](https://www.nuget.org/packages/WopiHost.Cobalt)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Cobalt.svg)](https://www.nuget.org/packages/WopiHost.Cobalt)

A .NET library providing MS-FSSHTTP (Cobalt) protocol support for WOPI (Web Application Open Platform Interface) hosts. This package enables enhanced performance and compatibility with Office Web Apps 2013+ features through the Cobalt protocol.

## Features

- **MS-FSSHTTP Protocol**: Complete implementation of the Cobalt protocol for efficient document operations
- **Session Management**: Handle Cobalt sessions for document editing
- **Lock Integration**: Seamless integration with WOPI lock providers
- **Performance Optimization**: Enhanced performance for large documents and complex operations
- **Office Compatibility**: Full compatibility with Office Web Apps 2013+ features

## Installation

```bash
dotnet add package WopiHost.Cobalt
```

## Prerequisites

**Important**: This package requires the `Microsoft.CobaltCore.dll` library, which is part of Office Web Apps 2013 / Office Online Server 2016. This DLL is not included in the package due to licensing restrictions.

### Obtaining Microsoft.CobaltCore.dll

The `Microsoft.CobaltCore.dll` library is part of Office Online Server, Office Web Apps, and SharePoint. Due to licensing restrictions, it cannot be distributed publicly.

**⚠️ Important**: Always ensure your OWA/OOS server and users have valid licenses before using it.

### Creating the Required NuGet Package

For detailed instructions on creating your own `Microsoft.CobaltCore` NuGet package, including:

- **Locating the DLL** in Office Online Server installations
- **Porting to .NET Standard/.NET Core** for modern projects
- **Creating .NET Framework packages** for legacy projects
- **Step-by-step decompilation process** using ILSpy and dotPeek
- **Complete project files and configurations**

**📖 [Follow the complete guide →](https://github.com/petrsvihlik/WopiHost/wiki/Craft-your-own-Microsoft.CobaltCore-NuGet-package)**

## Quick Start

### Basic Setup

```csharp
using WopiHost.Cobalt;
using WopiHost.Core.Extensions;

// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure WOPI with Cobalt support
builder.Services.Configure<WopiHostOptions>(options =>
{
    options.UseCobalt = true; // Enable Cobalt support
    options.ClientUrl = new Uri("https://your-office-online-server.com");
});

// Register Cobalt services
builder.Services.AddCobalt();

// Add WOPI
builder.Services.AddWopi();

var app = builder.Build();
app.Run();
```

### With Custom Configuration

```csharp
// Configure Cobalt-specific options
builder.Services.Configure<CobaltOptions>(options =>
{
    options.SessionTimeout = TimeSpan.FromHours(2);
    options.MaxConcurrentSessions = 100;
    options.EnablePerformanceOptimizations = true;
});

// Register services
builder.Services.AddCobalt();
builder.Services.AddWopi();
```

## Hero Scenarios

### 1. High-Performance Document Editing

Enable Cobalt for enhanced performance with large documents:

```csharp
public class HighPerformanceWopiHost
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure for high performance
        builder.Services.Configure<WopiHostOptions>(options =>
        {
            options.UseCobalt = true;
            options.ClientUrl = new Uri("https://office-online-server.com");
        });
        
        builder.Services.Configure<CobaltOptions>(options =>
        {
            options.SessionTimeout = TimeSpan.FromHours(4);
            options.MaxConcurrentSessions = 200;
            options.EnablePerformanceOptimizations = true;
        });
        
        // Register services
        builder.Services.AddCobalt();
        builder.Services.AddWopi();
        
        var app = builder.Build();
        app.Run();
    }
}
```

### 2. Enterprise Document Management

Integrate Cobalt with enterprise document management systems:

```csharp
public class EnterpriseCobaltService
{
    private readonly ICobaltSessionManager _sessionManager;
    private readonly IWopiLockProvider _lockProvider;
    
    public async Task<CobaltSession> CreateDocumentSessionAsync(string documentId, string userId)
    {
        // Create Cobalt session for document editing
        var session = await _sessionManager.CreateSessionAsync(documentId, userId);
        
        // Integrate with enterprise lock management
        var lockId = await _lockProvider.LockAsync(documentId, $"cobalt-{session.SessionId}", TimeSpan.FromHours(2));
        
        if (!lockId)
        {
            throw new InvalidOperationException("Failed to acquire document lock");
        }
        
        return session;
    }
    
    public async Task CloseDocumentSessionAsync(CobaltSession session)
    {
        // Release enterprise lock
        await _lockProvider.UnlockAsync(session.DocumentId, $"cobalt-{session.SessionId}");
        
        // Close Cobalt session
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }
}
```

### 3. Multi-Tenant Cobalt Host

Build a multi-tenant WOPI host with Cobalt support:

```csharp
public class MultiTenantCobaltHost
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Add multi-tenant services
        builder.Services.AddMultiTenant<TenantInfo>()
            .WithConfigurationStore()
            .WithRouteStrategy();
        
        // Configure Cobalt per tenant
        builder.Services.AddScoped<ICobaltSessionManager>(provider =>
        {
            var tenantContext = provider.GetRequiredService<ITenantContextAccessor<TenantInfo>>();
            var tenant = tenantContext.TenantInfo;
            
            return new TenantAwareCobaltSessionManager(tenant);
        });
        
        // Register services
        builder.Services.AddCobalt();
        builder.Services.AddWopi();
        
        var app = builder.Build();
        app.Run();
    }
}

public class TenantAwareCobaltSessionManager : ICobaltSessionManager
{
    private readonly TenantInfo _tenant;
    
    public TenantAwareCobaltSessionManager(TenantInfo tenant)
    {
        _tenant = tenant;
    }
    
    public async Task<CobaltSession> CreateSessionAsync(string documentId, string userId)
    {
        // Create tenant-aware Cobalt session
        var sessionId = $"{_tenant.Id}-{Guid.NewGuid()}";
        return new CobaltSession(sessionId, documentId, userId);
    }
    
    // Implement other required methods...
}
```

## API Reference

### ICobaltSessionManager

Manages Cobalt sessions for document editing.

#### Methods

##### CreateSessionAsync

```csharp
Task<CobaltSession> CreateSessionAsync(string documentId, string userId)
```

Creates a new Cobalt session for document editing.

**Parameters:**
- `documentId`: Document identifier
- `userId`: User identifier

**Returns:** New Cobalt session

##### CloseSessionAsync

```csharp
Task CloseSessionAsync(string sessionId)
```

Closes a Cobalt session.

**Parameters:**
- `sessionId`: Session identifier to close

### CobaltSession

Represents a Cobalt editing session.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `SessionId` | `string` | Unique session identifier |
| `DocumentId` | `string` | Document being edited |
| `UserId` | `string` | User editing the document |
| `CreatedAt` | `DateTime` | Session creation time |
| `LastActivity` | `DateTime` | Last activity time |

### CobaltOptions

Configuration options for Cobalt functionality.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `SessionTimeout` | `TimeSpan` | Default session timeout |
| `MaxConcurrentSessions` | `int` | Maximum concurrent sessions |
| `EnablePerformanceOptimizations` | `bool` | Enable performance optimizations |

## Configuration

### appsettings.json

```json
{
  "Wopi": {
    "UseCobalt": true,
    "ClientUrl": "https://your-office-online-server.com"
  },
  "Cobalt": {
    "SessionTimeout": "02:00:00",
    "MaxConcurrentSessions": 100,
    "EnablePerformanceOptimizations": true
  }
}
```

### Program.cs

```csharp
builder.Services.Configure<WopiHostOptions>(options =>
{
    options.UseCobalt = builder.Configuration.GetValue<bool>("Wopi:UseCobalt");
    options.ClientUrl = new Uri(builder.Configuration["Wopi:ClientUrl"]);
});

builder.Services.Configure<CobaltOptions>(options =>
{
    builder.Configuration.GetSection("Cobalt").Bind(options);
});

builder.Services.AddCobalt();
builder.Services.AddWopi();
```

## Performance Benefits

### When to Use Cobalt

**✅ Use Cobalt when:**
- Working with large documents (>10MB)
- Need enhanced performance for complex operations
- Using Office Web Apps 2013+ features
- Require better memory management
- Need improved concurrent editing support

**❌ Don't use Cobalt when:**
- Working with simple, small documents
- Office Web Apps 2013+ is not available
- Performance is not a critical concern
- Microsoft.CobaltCore.dll is not available

### Performance Characteristics

- **Memory Usage**: More efficient memory usage for large documents
- **Network Traffic**: Reduced network traffic for complex operations
- **Concurrent Editing**: Better support for multiple users editing the same document
- **Response Time**: Faster response times for large document operations

## Troubleshooting

### Common Issues

1. **Microsoft.CobaltCore.dll Not Found**
   - Ensure the DLL is available in your project
   - Create the required NuGet package
   - Check that the DLL is properly referenced

2. **Cobalt Sessions Not Working**
   - Verify that `UseCobalt` is set to `true`
   - Check that the Office Online Server supports Cobalt
   - Ensure proper session management implementation

3. **Performance Issues**
   - Enable performance optimizations in `CobaltOptions`
   - Monitor session timeout settings
   - Check concurrent session limits

### Debugging

Enable detailed logging for Cobalt operations:

```json
{
  "Logging": {
    "LogLevel": {
      "WopiHost.Cobalt": "Debug"
    }
  }
}
```

## Dependencies

- `WopiHost.Abstractions`: Core WOPI interfaces
- `WopiHost.Core`: WOPI core functionality
- `Microsoft.CobaltCore`: Microsoft Cobalt Core library (not included)

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.txt) file for details.

**Note**: This module includes code developed by Marx Yu (https://github.com/marx-yu). See also the file [ORIGINAL_WORK_LICENSE.txt](ORIGINAL_WORK_LICENSE.txt).

## Contributing

Contributions are welcome! Please read our [Contributing Guidelines](../../CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Support

For support and questions:
- Create an issue on [GitHub](https://github.com/petrsvihlik/WopiHost/issues)
- Check the [documentation](https://github.com/petrsvihlik/WopiHost)
- Review the [WOPI specification](https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery)
