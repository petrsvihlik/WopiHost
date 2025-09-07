# WopiHost.Url

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Url.svg)](https://www.nuget.org/packages/WopiHost.Url)

A .NET library for generating WOPI (Web Application Open Platform Interface) URLs according to the Microsoft specification. This package provides utilities to create properly formatted URLs for Office Online Server integration.

## Features

- **WOPI URL Generation**: Generate compliant WOPI URLs for Office Online Server
- **Discovery Integration**: Works seamlessly with WOPI discovery services
- **URL Parameter Management**: Handle optional and mandatory URL parameters
- **Template Resolution**: Resolve URL templates with proper parameter substitution
- **Culture Support**: Built-in support for UI and data culture settings

## Installation

```bash
dotnet add package WopiHost.Url
```

## Quick Start

### Basic Usage

```csharp
using WopiHost.Url;
using WopiHost.Discovery;

// Create a discoverer (you'll need WopiHost.Discovery package)
var discoverer = new WopiDiscoverer(discoveryFileProvider, options);

// Create URL builder
var urlBuilder = new WopiUrlBuilder(discoverer);

// Generate a WOPI URL for editing a Word document
var fileUrl = new Uri("https://your-wopi-host.com/wopi/files/document123");
var wopiUrl = await urlBuilder.GetFileUrlAsync("docx", fileUrl, WopiActionEnum.Edit);

Console.WriteLine($"WOPI URL: {wopiUrl}");
```

### Advanced Usage with Custom Settings

```csharp
// Create URL settings with custom parameters
var urlSettings = new WopiUrlSettings
{
    UiLlcc = new CultureInfo("en-US"),
    DcLlcc = new CultureInfo("en-US"),
    Embedded = true,
    DisablePrint = false,
    DisableTranslation = false,
    SessionContext = "user-session-123"
};

// Create URL builder with settings
var urlBuilder = new WopiUrlBuilder(discoverer, urlSettings);

// Generate URL for specific action
var editUrl = await urlBuilder.GetFileUrlAsync("xlsx", fileUrl, WopiActionEnum.Edit);
var viewUrl = await urlBuilder.GetFileUrlAsync("pptx", fileUrl, WopiActionEnum.View);
```

## Hero Scenarios

### 1. Office Document Integration

Integrate Office documents into your web application with proper WOPI URLs:

```csharp
public class DocumentService
{
    private readonly WopiUrlBuilder _urlBuilder;
    
    public DocumentService(WopiUrlBuilder urlBuilder)
    {
        _urlBuilder = urlBuilder;
    }
    
    public async Task<string> GetEditUrlAsync(string documentId, string extension)
    {
        var fileUrl = new Uri($"https://myapp.com/wopi/files/{documentId}");
        return await _urlBuilder.GetFileUrlAsync(extension, fileUrl, WopiActionEnum.Edit);
    }
    
    public async Task<string> GetViewUrlAsync(string documentId, string extension)
    {
        var fileUrl = new Uri($"https://myapp.com/wopi/files/{documentId}");
        return await _urlBuilder.GetFileUrlAsync(extension, fileUrl, WopiActionEnum.View);
    }
}
```

### 2. Multi-Language Office Integration

Support multiple languages in your Office integration:

```csharp
public class LocalizedDocumentService
{
    private readonly WopiUrlBuilder _urlBuilder;
    
    public async Task<string> GetLocalizedUrlAsync(string documentId, string extension, 
        string uiLanguage, string dataLanguage)
    {
        var settings = new WopiUrlSettings
        {
            UiLlcc = new CultureInfo(uiLanguage),
            DcLlcc = new CultureInfo(dataLanguage)
        };
        
        var fileUrl = new Uri($"https://myapp.com/wopi/files/{documentId}");
        return await _urlBuilder.GetFileUrlAsync(extension, fileUrl, WopiActionEnum.Edit, settings);
    }
}
```

### 3. Embedded Office Viewer

Create embedded Office viewers for your web application:

```csharp
public class EmbeddedOfficeController : Controller
{
    private readonly WopiUrlBuilder _urlBuilder;
    
    public async Task<IActionResult> ViewDocument(string documentId, string extension)
    {
        var settings = new WopiUrlSettings
        {
            Embedded = true,
            DisablePrint = true,
            DisableTranslation = true
        };
        
        var fileUrl = new Uri($"https://myapp.com/wopi/files/{documentId}");
        var wopiUrl = await _urlBuilder.GetFileUrlAsync(extension, fileUrl, WopiActionEnum.View, settings);
        
        ViewBag.WopiUrl = wopiUrl;
        return View();
    }
}
```

## API Reference

### WopiUrlBuilder

The main class for generating WOPI URLs.

#### Constructor

```csharp
public WopiUrlBuilder(IDiscoverer discoverer, WopiUrlSettings? urlSettings = null)
```

- `discoverer`: Provider of WOPI discovery data
- `urlSettings`: Optional default URL settings

#### Methods

##### GetFileUrlAsync

```csharp
public async Task<string?> GetFileUrlAsync(string extension, Uri wopiFileUrl, WopiActionEnum action, WopiUrlSettings? urlSettings = null)
```

Generates a WOPI URL for the specified file and action.

**Parameters:**
- `extension`: File extension (e.g., "docx", "xlsx", "pptx")
- `wopiFileUrl`: URL of the file in your WOPI host
- `action`: WOPI action to perform (Edit, View, etc.)
- `urlSettings`: Optional URL settings (overrides default settings)

**Returns:** Generated WOPI URL or null if not supported

### WopiUrlSettings

Configuration class for URL parameters.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `UiLlcc` | `CultureInfo` | UI language culture |
| `DcLlcc` | `CultureInfo` | Data language culture |
| `Embedded` | `bool` | Embed in web page |
| `DisablePrint` | `bool` | Disable print functionality |
| `DisableTranslation` | `bool` | Disable translation |
| `SessionContext` | `string` | Session context for tracking |
| `HostSessionId` | `string` | Host session identifier |

## Dependencies

- `WopiHost.Discovery`: For WOPI discovery functionality
- `Microsoft.Extensions.Options`: For configuration support

## Supported File Extensions

- **Word**: .docx, .docm, .doc
- **Excel**: .xlsx, .xlsm, .xls
- **PowerPoint**: .pptx, .pptm, .ppt
- **OneNote**: .one
- **Visio**: .vsdx, .vsdm

## Supported Actions

- `Edit`: Edit the document
- `View`: View the document (read-only)
- `Attendee`: Attendee view for presentations
- `Present`: Present mode for presentations
- `EditNew`: Create new document
- `InteractiveEditNew`: Interactive new document creation

## Examples

### ASP.NET Core Integration

```csharp
// Program.cs
builder.Services.AddWopiDiscovery<WopiHostOptions>(options => 
    builder.Configuration.GetSection("Wopi:Discovery").Bind(options));

builder.Services.AddScoped<WopiUrlBuilder>();

// Controller
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly WopiUrlBuilder _urlBuilder;
    
    public DocumentsController(WopiUrlBuilder urlBuilder)
    {
        _urlBuilder = urlBuilder;
    }
    
    [HttpGet("{id}/edit")]
    public async Task<IActionResult> GetEditUrl(string id, string extension)
    {
        var fileUrl = new Uri($"https://myapp.com/wopi/files/{id}");
        var wopiUrl = await _urlBuilder.GetFileUrlAsync(extension, fileUrl, WopiActionEnum.Edit);
        
        return Ok(new { Url = wopiUrl });
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
