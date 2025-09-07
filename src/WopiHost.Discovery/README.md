# WopiHost.Discovery

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Discovery.svg)](https://www.nuget.org/packages/WopiHost.Discovery)

A .NET library for discovering WOPI (Web Application Open Platform Interface) client capabilities. This package provides functionality to parse and query WOPI discovery XML files to determine supported file types, actions, and requirements.

## Features

- **WOPI Discovery Parsing**: Parse WOPI discovery XML files from Office Online Server
- **Capability Detection**: Determine supported file extensions and actions
- **URL Template Resolution**: Get URL templates for specific file types and actions
- **Requirement Analysis**: Check WOPI host requirements for specific actions
- **Caching Support**: Built-in caching with configurable refresh intervals
- **Multiple Providers**: Support for HTTP and file system discovery providers

## Installation

```bash
dotnet add package WopiHost.Discovery
```

## Quick Start

### Basic Usage

```csharp
using WopiHost.Discovery;
using Microsoft.Extensions.Options;

// Configure discovery options
var discoveryOptions = new DiscoveryOptions
{
    NetZone = NetZoneEnum.ExternalHttp,
    RefreshInterval = TimeSpan.FromHours(24)
};

// Create HTTP discovery provider
var httpProvider = new HttpDiscoveryFileProvider(httpClient, options);

// Create discoverer
var discoverer = new WopiDiscoverer(httpProvider, Options.Create(discoveryOptions));

// Check if a file extension is supported
bool isSupported = await discoverer.SupportsExtensionAsync("docx");
Console.WriteLine($"Word documents supported: {isSupported}");

// Get URL template for editing a Word document
string? editTemplate = await discoverer.GetUrlTemplateAsync("docx", WopiActionEnum.Edit);
Console.WriteLine($"Edit template: {editTemplate}");
```

### ASP.NET Core Integration

```csharp
// Program.cs
builder.Services.AddWopiDiscovery<WopiHostOptions>(options =>
{
    options.NetZone = NetZoneEnum.ExternalHttp;
    options.RefreshInterval = TimeSpan.FromHours(24);
});

// Usage in controller
[ApiController]
public class DiscoveryController : ControllerBase
{
    private readonly IDiscoverer _discoverer;
    
    public DiscoveryController(IDiscoverer discoverer)
    {
        _discoverer = discoverer;
    }
    
    [HttpGet("capabilities/{extension}")]
    public async Task<IActionResult> GetCapabilities(string extension)
    {
        var isSupported = await _discoverer.SupportsExtensionAsync(extension);
        var requirements = await _discoverer.GetActionRequirementsAsync(extension, WopiActionEnum.Edit);
        
        return Ok(new { Supported = isSupported, Requirements = requirements });
    }
}
```

## Hero Scenarios

### 1. Dynamic Office Integration

Build a dynamic Office integration that adapts based on WOPI client capabilities:

```csharp
public class DynamicOfficeService
{
    private readonly IDiscoverer _discoverer;
    
    public DynamicOfficeService(IDiscoverer discoverer)
    {
        _discoverer = discoverer;
    }
    
    public async Task<OfficeIntegrationInfo> GetIntegrationInfoAsync(string fileExtension)
    {
        var isSupported = await _discoverer.SupportsExtensionAsync(fileExtension);
        
        if (!isSupported)
        {
            return new OfficeIntegrationInfo { IsSupported = false };
        }
        
        var canEdit = await _discoverer.SupportsActionAsync(fileExtension, WopiActionEnum.Edit);
        var canView = await _discoverer.SupportsActionAsync(fileExtension, WopiActionEnum.View);
        var requirements = await _discoverer.GetActionRequirementsAsync(fileExtension, WopiActionEnum.Edit);
        
        return new OfficeIntegrationInfo
        {
            IsSupported = true,
            CanEdit = canEdit,
            CanView = canView,
            Requirements = requirements,
            EditTemplate = canEdit ? await _discoverer.GetUrlTemplateAsync(fileExtension, WopiActionEnum.Edit) : null,
            ViewTemplate = canView ? await _discoverer.GetUrlTemplateAsync(fileExtension, WopiActionEnum.View) : null
        };
    }
}

public class OfficeIntegrationInfo
{
    public bool IsSupported { get; set; }
    public bool CanEdit { get; set; }
    public bool CanView { get; set; }
    public IEnumerable<string> Requirements { get; set; } = [];
    public string? EditTemplate { get; set; }
    public string? ViewTemplate { get; set; }
}
```

### 2. File Upload Validation

Validate uploaded files against WOPI client capabilities:

```csharp
public class FileUploadService
{
    private readonly IDiscoverer _discoverer;
    
    public async Task<ValidationResult> ValidateFileAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).TrimStart('.');
        
        var isSupported = await _discoverer.SupportsExtensionAsync(extension);
        if (!isSupported)
        {
            return ValidationResult.Failure($"File type .{extension} is not supported by the WOPI client");
        }
        
        var canEdit = await _discoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);
        var canView = await _discoverer.SupportsActionAsync(extension, WopiActionEnum.View);
        
        return ValidationResult.Success(new FileCapabilities
        {
            Extension = extension,
            CanEdit = canEdit,
            CanView = canView,
            Requirements = await _discoverer.GetActionRequirementsAsync(extension, WopiActionEnum.Edit)
        });
    }
}
```

### 3. Office Client Health Monitoring

Monitor Office client health and capabilities:

```csharp
public class OfficeClientHealthService
{
    private readonly IDiscoverer _discoverer;
    private readonly ILogger<OfficeClientHealthService> _logger;
    
    public async Task<HealthStatus> CheckOfficeClientHealthAsync()
    {
        try
        {
            // Test common file types
            var commonExtensions = new[] { "docx", "xlsx", "pptx", "pdf" };
            var supportedExtensions = new List<string>();
            
            foreach (var extension in commonExtensions)
            {
                if (await _discoverer.SupportsExtensionAsync(extension))
                {
                    supportedExtensions.Add(extension);
                }
            }
            
            var healthStatus = new HealthStatus
            {
                IsHealthy = supportedExtensions.Count > 0,
                SupportedExtensions = supportedExtensions,
                Timestamp = DateTime.UtcNow
            };
            
            if (!healthStatus.IsHealthy)
            {
                _logger.LogWarning("Office client health check failed - no supported extensions found");
            }
            
            return healthStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Office client health check failed");
            return new HealthStatus
            {
                IsHealthy = false,
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
```

## API Reference

### IDiscoverer

Main interface for WOPI discovery functionality.

#### Methods

##### GetUrlTemplateAsync

```csharp
Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action)
```

Gets the URL template for a specific file extension and action.

**Parameters:**
- `extension`: File extension (without leading dot)
- `action`: WOPI action to perform

**Returns:** URL template with placeholders or null if not supported

##### SupportsExtensionAsync

```csharp
Task<bool> SupportsExtensionAsync(string extension)
```

Determines if a file extension is supported by the WOPI client.

**Parameters:**
- `extension`: File extension to check (without leading dot)

**Returns:** True if the extension is supported

##### SupportsActionAsync

```csharp
Task<bool> SupportsActionAsync(string extension, WopiActionEnum action)
```

Determines if a specific action is supported for a file extension.

**Parameters:**
- `extension`: File extension to check
- `action`: Action to check support for

**Returns:** True if the action is supported for the extension

##### GetActionRequirementsAsync

```csharp
Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action)
```

Gets the WOPI host requirements for a specific action and file extension.

**Parameters:**
- `extension`: File extension to check
- `action`: Action to get requirements for

**Returns:** Collection of requirement strings

##### RequiresCobaltAsync

```csharp
Task<bool> RequiresCobaltAsync(string extension, WopiActionEnum action)
```

Determines if MS-FSSHTTP (Cobalt) is required for the action.

**Parameters:**
- `extension`: File extension to check
- `action`: Action to check

**Returns:** True if Cobalt is required

### DiscoveryOptions

Configuration options for the discovery service.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `NetZone` | `NetZoneEnum` | Network zone (InternalHttp, ExternalHttp, InternalHttps, ExternalHttps) |
| `RefreshInterval` | `TimeSpan` | How often to refresh the discovery file (default: 24 hours) |

### IDiscoveryFileProvider

Interface for providing WOPI discovery XML files.

#### Implementations

- **HttpDiscoveryFileProvider**: Fetches discovery file via HTTP
- **FileSystemDiscoveryFileProvider**: Reads discovery file from local file system

## Configuration

### appsettings.json

```json
{
  "Wopi": {
    "Discovery": {
      "NetZone": "ExternalHttp",
      "RefreshInterval": "24:00:00"
    }
  }
}
```

### Program.cs

```csharp
builder.Services.AddWopiDiscovery<WopiHostOptions>(options =>
{
    options.NetZone = NetZoneEnum.ExternalHttp;
    options.RefreshInterval = TimeSpan.FromHours(24);
});
```

## Supported File Extensions

The discovery service can detect support for various Office file formats:

- **Word**: .docx, .docm, .doc
- **Excel**: .xlsx, .xlsm, .xls
- **PowerPoint**: .pptx, .pptm, .ppt
- **OneNote**: .one
- **Visio**: .vsdx, .vsdm
- **PDF**: .pdf (view-only)

## Supported Actions

- `Edit`: Edit the document
- `View`: View the document (read-only)
- `Attendee`: Attendee view for presentations
- `Present`: Present mode for presentations
- `EditNew`: Create new document
- `InteractiveEditNew`: Interactive new document creation

## Error Handling

The discovery service includes comprehensive error handling:

```csharp
try
{
    var isSupported = await discoverer.SupportsExtensionAsync("docx");
}
catch (DiscoveryException ex)
{
    // Handle discovery-specific errors
    logger.LogError(ex, "Discovery failed: {Message}", ex.Message);
}
catch (HttpRequestException ex)
{
    // Handle HTTP-related errors
    logger.LogError(ex, "Failed to fetch discovery file");
}
```

## Caching

The discovery service includes built-in caching to improve performance:

- Discovery XML files are cached for the configured refresh interval
- Automatic refresh when cache expires
- Thread-safe caching implementation
- Configurable cache duration

## Dependencies

- `WopiHost.Abstractions`: For WOPI abstractions
- `Microsoft.Extensions.Options`: For configuration support
- `Microsoft.Extensions.Http`: For HTTP client support

## Examples

### Custom Discovery Provider

```csharp
public class CustomDiscoveryProvider : IDiscoveryFileProvider
{
    public async Task<XElement> GetDiscoveryXmlAsync()
    {
        // Custom logic to fetch discovery XML
        var xmlContent = await FetchDiscoveryXmlFromCustomSource();
        return XElement.Parse(xmlContent);
    }
}

// Registration
services.AddSingleton<IDiscoveryFileProvider, CustomDiscoveryProvider>();
services.AddSingleton<IDiscoverer, WopiDiscoverer>();
```

### Health Check Integration

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<WopiDiscoveryHealthCheck>("wopi-discovery");

public class WopiDiscoveryHealthCheck : IHealthCheck
{
    private readonly IDiscoverer _discoverer;
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _discoverer.SupportsExtensionAsync("docx");
            return isHealthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("WOPI discovery unhealthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("WOPI discovery error", ex);
        }
    }
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
