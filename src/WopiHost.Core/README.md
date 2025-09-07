# WopiHost.Core

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Core.svg)](https://www.nuget.org/packages/WopiHost.Core)

A .NET library providing the core WOPI (Web Application Open Platform Interface) server implementation. This package contains ASP.NET Core controllers, middleware, and services that implement the WOPI protocol for integrating with Office Online Server.

## Features

- **WOPI Controllers**: Complete implementation of WOPI REST API endpoints
- **Authentication & Authorization**: Built-in WOPI token authentication and authorization
- **File Operations**: Support for all standard WOPI file operations
- **Container Operations**: Folder and container management
- **Lock Management**: File locking and concurrency control
- **Security Validation**: WOPI proof validation and origin checking
- **Extensible Architecture**: Easy integration with custom storage providers

## Installation

```bash
dotnet add package WopiHost.Core
```

## Quick Start

### Basic Setup

```csharp
using WopiHost.Core.Extensions;
using WopiHost.Core.Models;

// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure WOPI options
builder.Services.Configure<WopiHostOptions>(options =>
{
    options.ClientUrl = new Uri("https://your-office-online-server.com");
    options.StorageProviderAssemblyName = "YourStorageProvider";
    options.UseCobalt = false; // Set to true if using MS-FSSHTTP
});

// Add WOPI services
builder.Services.AddWopi();

// Add your custom storage provider
builder.Services.AddScoped<IWopiStorageProvider, YourStorageProvider>();

var app = builder.Build();

// Configure pipeline
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Configuration

```json
{
  "Wopi": {
    "ClientUrl": "https://your-office-online-server.com",
    "StorageProviderAssemblyName": "YourStorageProvider",
    "LockProviderAssemblyName": "YourLockProvider",
    "UseCobalt": false,
    "Discovery": {
      "NetZone": "ExternalHttp",
      "RefreshInterval": "24:00:00"
    }
  }
}
```

## Hero Scenarios

### 1. Complete WOPI Host Implementation

Build a complete WOPI host with custom storage:

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure services
        builder.Services.AddControllers();
        builder.Services.AddWopi();
        
        // Configure WOPI options
        builder.Services.Configure<WopiHostOptions>(options =>
        {
            options.ClientUrl = new Uri("https://office-online-server.com");
            options.StorageProviderAssemblyName = "MyStorageProvider";
            options.LockProviderAssemblyName = "MyLockProvider";
            options.UseCobalt = true;
        });
        
        // Add custom storage provider
        builder.Services.AddScoped<IWopiStorageProvider, MyStorageProvider>();
        builder.Services.AddScoped<IWopiWritableStorageProvider, MyStorageProvider>();
        builder.Services.AddScoped<IWopiLockProvider, MyLockProvider>();
        builder.Services.AddScoped<IWopiSecurityHandler, MySecurityHandler>();
        
        // Add discovery services
        builder.Services.AddWopiDiscovery<WopiHostOptions>(options =>
        {
            options.NetZone = NetZoneEnum.ExternalHttp;
            options.RefreshInterval = TimeSpan.FromHours(24);
        });
        
        var app = builder.Build();
        
        // Configure pipeline
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        
        app.Run();
    }
}

// Custom storage provider implementation
public class MyStorageProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    // Implement storage operations
    public Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        // Your implementation
        throw new NotImplementedException();
    }
    
    // Implement other required methods...
}
```

### 2. Enterprise Document Management

Integrate WOPI with enterprise document management systems:

```csharp
public class EnterpriseDocumentController : ControllerBase
{
    private readonly IWopiStorageProvider _storageProvider;
    private readonly IDocumentService _documentService;
    private readonly IUserService _userService;
    
    [HttpGet("documents/{documentId}/wopi-url")]
    public async Task<IActionResult> GetWopiUrl(string documentId, [FromQuery] string action = "edit")
    {
        // Get document from enterprise system
        var document = await _documentService.GetDocumentAsync(documentId);
        if (document == null)
        {
            return NotFound();
        }
        
        // Check user permissions
        var user = await _userService.GetCurrentUserAsync();
        if (!await _userService.HasPermissionAsync(user.Id, documentId, action))
        {
            return Forbid();
        }
        
        // Generate WOPI URL
        var wopiUrl = GenerateWopiUrl(documentId, action);
        
        return Ok(new { WopiUrl = wopiUrl, Document = document });
    }
    
    [HttpPost("documents/{documentId}/checkout")]
    public async Task<IActionResult> CheckoutDocument(string documentId)
    {
        var document = await _documentService.GetDocumentAsync(documentId);
        if (document == null)
        {
            return NotFound();
        }
        
        // Implement checkout logic
        await _documentService.CheckoutDocumentAsync(documentId);
        
        return Ok();
    }
    
    [HttpPost("documents/{documentId}/checkin")]
    public async Task<IActionResult> CheckinDocument(string documentId, [FromBody] CheckinRequest request)
    {
        var document = await _documentService.GetDocumentAsync(documentId);
        if (document == null)
        {
            return NotFound();
        }
        
        // Implement checkin logic
        await _documentService.CheckinDocumentAsync(documentId, request.Comment);
        
        return Ok();
    }
}
```

### 3. Multi-Tenant WOPI Host

Build a multi-tenant WOPI host with tenant isolation:

```csharp
public class MultiTenantWopiHost
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Add multi-tenant services
        builder.Services.AddMultiTenant<TenantInfo>()
            .WithConfigurationStore()
            .WithRouteStrategy();
        
        // Add WOPI with tenant-aware configuration
        builder.Services.AddWopi();
        
        // Add tenant-aware storage provider
        builder.Services.AddScoped<IWopiStorageProvider>(provider =>
        {
            var tenantContext = provider.GetRequiredService<ITenantContextAccessor<TenantInfo>>();
            var tenant = tenantContext.TenantInfo;
            
            return new TenantAwareStorageProvider(tenant);
        });
        
        // Add tenant-aware security handler
        builder.Services.AddScoped<IWopiSecurityHandler, TenantAwareSecurityHandler>();
        
        var app = builder.Build();
        
        // Configure multi-tenant pipeline
        app.UseMultiTenant();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        
        app.Run();
    }
}

public class TenantAwareStorageProvider : IWopiStorageProvider
{
    private readonly TenantInfo _tenant;
    private readonly IStorageProviderFactory _storageFactory;
    
    public TenantAwareStorageProvider(TenantInfo tenant, IStorageProviderFactory storageFactory)
    {
        _tenant = tenant;
        _storageFactory = storageFactory;
    }
    
    public async Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        // Ensure tenant isolation
        var tenantAwareId = $"{_tenant.Id}/{identifier}";
        
        var storageProvider = _storageFactory.GetProvider(_tenant.StorageProviderType);
        return await storageProvider.GetWopiResource<T>(tenantAwareId, cancellationToken);
    }
    
    // Implement other methods with tenant isolation...
}
```

## API Reference

### Controllers

#### FilesController

Handles WOPI file operations.

**Endpoints:**
- `GET /wopi/files/{fileId}` - Get file content
- `POST /wopi/files/{fileId}` - Update file content
- `GET /wopi/files/{fileId}/contents` - Get file contents
- `POST /wopi/files/{fileId}/contents` - Update file contents
- `POST /wopi/files/{fileId}/lock` - Lock file
- `POST /wopi/files/{fileId}/unlock` - Unlock file
- `GET /wopi/files/{fileId}/lock` - Get file lock
- `POST /wopi/files/{fileId}/refreshlock` - Refresh file lock
- `POST /wopi/files/{fileId}/rename` - Rename file
- `DELETE /wopi/files/{fileId}` - Delete file

#### ContainersController

Handles WOPI container/folder operations.

**Endpoints:**
- `GET /wopi/containers/{containerId}` - Get container info
- `GET /wopi/containers/{containerId}/children` - Get container children
- `POST /wopi/containers/{containerId}` - Create container
- `DELETE /wopi/containers/{containerId}` - Delete container

#### EcosystemController

Handles WOPI ecosystem operations.

**Endpoints:**
- `GET /wopi/ecosystem` - Get ecosystem info

### Models

#### WopiHostOptions

Configuration options for the WOPI host.

```csharp
public class WopiHostOptions : IDiscoveryOptions
{
    public bool UseCobalt { get; set; }
    public required string StorageProviderAssemblyName { get; set; }
    public string? LockProviderAssemblyName { get; set; }
    public Func<WopiCheckFileInfoContext, Task<WopiCheckFileInfo>> OnCheckFileInfo { get; set; }
    public Func<WopiCheckContainerInfoContext, Task<WopiCheckContainerInfo>> OnCheckContainerInfo { get; set; }
    public required Uri ClientUrl { get; set; }
}
```

### Extensions

#### ServiceCollectionExtensions

Extension methods for registering WOPI services.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWopi(this IServiceCollection services);
    public static IServiceCollection AddWopi(this IServiceCollection services, Action<WopiHostOptions> configureOptions);
}
```

## Security

### Authentication

WOPI uses token-based authentication. Implement `IWopiSecurityHandler` to provide custom authentication:

```csharp
public class CustomSecurityHandler : IWopiSecurityHandler
{
    public async Task<WopiUserPermissions> GetUserPermissionsAsync(string userId, string resourceId)
    {
        // Implement permission logic
        return WopiUserPermissions.Read | WopiUserPermissions.Write;
    }
    
    public async Task<bool> ValidateTokenAsync(string token, string resourceId)
    {
        // Implement token validation
        return true;
    }
    
    public async Task<string> GetUserIdAsync(string token)
    {
        // Extract user ID from token
        return "user123";
    }
}
```

### Authorization

WOPI includes built-in authorization policies. Customize by implementing `IWopiAuthorizationRequirement`:

```csharp
public class CustomAuthorizationRequirement : IWopiAuthorizationRequirement
{
    public string RequiredPermission { get; set; }
    
    public bool IsSatisfiedBy(WopiUserPermissions permissions)
    {
        return permissions.HasFlag(WopiUserPermissions.Write);
    }
}
```

## Configuration

### appsettings.json

```json
{
  "Wopi": {
    "ClientUrl": "https://your-office-online-server.com",
    "StorageProviderAssemblyName": "YourStorageProvider",
    "LockProviderAssemblyName": "YourLockProvider",
    "UseCobalt": false,
    "Discovery": {
      "NetZone": "ExternalHttp",
      "RefreshInterval": "24:00:00"
    }
  },
  "Logging": {
    "LogLevel": {
      "WopiHost.Core": "Information"
    }
  }
}
```

### Environment Variables

```bash
WOPI__CLIENTURL=https://your-office-online-server.com
WOPI__STORAGEPROVIDERASSEMBLYNAME=YourStorageProvider
WOPI__USECOBALT=false
```

## Middleware

### WopiOriginValidationActionFilter

Validates WOPI request origins to prevent unauthorized access.

```csharp
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
public class FilesController : ControllerBase
{
    // Controller implementation
}
```

## Error Handling

WOPI Core includes comprehensive error handling:

```csharp
public class WopiErrorHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is WopiException wopiException)
        {
            httpContext.Response.StatusCode = wopiException.StatusCode;
            await httpContext.Response.WriteAsync(wopiException.Message, cancellationToken);
            return true;
        }
        
        return false;
    }
}
```

## Logging

WOPI Core includes structured logging:

```csharp
// Configure Serilog for WOPI
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("WopiHost.Core", LogEventLevel.Debug)
    .WriteTo.Console()
    .CreateLogger();

// In your Program.cs
builder.Host.UseSerilog();
```

## Health Checks

Add WOPI health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<WopiHealthCheck>("wopi");

public class WopiHealthCheck : IHealthCheck
{
    private readonly IWopiStorageProvider _storageProvider;
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check storage provider health
            var rootContainer = _storageProvider.RootContainerPointer;
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("WOPI storage provider unhealthy", ex);
        }
    }
}
```

## Dependencies

- `WopiHost.Abstractions`: Core WOPI interfaces
- `WopiHost.Discovery`: WOPI discovery services
- `Microsoft.AspNetCore.App`: ASP.NET Core framework
- `Microsoft.Extensions.Configuration.Binder`: Configuration binding
- `System.IdentityModel.Tokens.Jwt`: JWT token handling

## Examples

### Custom File Operations

```csharp
[ApiController]
[Route("api/[controller]")]
public class CustomFileController : ControllerBase
{
    private readonly IWopiStorageProvider _storageProvider;
    
    [HttpPost("files/{fileId}/custom-operation")]
    public async Task<IActionResult> CustomOperation(string fileId, [FromBody] CustomOperationRequest request)
    {
        var file = await _storageProvider.GetWopiResource<IWopiFile>(fileId);
        if (file == null)
        {
            return NotFound();
        }
        
        // Implement custom operation
        var result = await PerformCustomOperation(file, request);
        
        return Ok(result);
    }
}
```

### Custom Middleware

```csharp
public class CustomWopiMiddleware
{
    private readonly RequestDelegate _next;
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Add custom WOPI headers
        context.Response.Headers.Add("X-WOPI-Custom-Header", "CustomValue");
        
        await _next(context);
    }
}

// Register in Program.cs
app.UseMiddleware<CustomWopiMiddleware>();
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
