# WopiHost.FileSystemProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.FileSystemProvider.svg)](https://www.nuget.org/packages/WopiHost.FileSystemProvider)

A .NET library providing a file system-based implementation of WOPI (Web Application Open Platform Interface) storage providers. This package implements `IWopiStorageProvider` and `IWopiWritableStorageProvider` using the local file system, making it easy to get started with WOPI host development.

## Features

- **File System Storage**: Complete file system-based WOPI storage implementation
- **Read/Write Operations**: Full support for both read and write operations
- **File Locking**: Built-in file locking using Windows file system access control
- **Security Integration**: JWT-based authentication and authorization
- **Base64 Encoding**: Secure file identifier encoding for file paths
- **In-Memory Caching**: Efficient file ID to path mapping
- **Windows ACL Support**: Integration with Windows Access Control Lists

## Installation

```bash
dotnet add package WopiHost.FileSystemProvider
```

## Quick Start

### Basic Setup

```csharp
using WopiHost.FileSystemProvider;
using WopiHost.Core.Extensions;

// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure file system provider
builder.Services.Configure<WopiFileSystemProviderOptions>(options =>
{
    options.RootPath = "C:\\WopiFiles"; // Your file system root
});

// Register services
builder.Services.AddSingleton<InMemoryFileIds>();
builder.Services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();
builder.Services.AddScoped<IWopiWritableStorageProvider, WopiFileSystemProvider>();
builder.Services.AddScoped<IWopiSecurityHandler, WopiSecurityHandler>();

// Add WOPI
builder.Services.AddWopi();

var app = builder.Build();
app.Run();
```

### Configuration

```json
{
  "Wopi": {
    "StorageOptions": {
      "RootPath": "C:\\WopiFiles"
    }
  }
}
```

## Hero Scenarios

### 1. Local Development WOPI Host

Set up a local WOPI host for development and testing:

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure file system provider
        builder.Services.Configure<WopiFileSystemProviderOptions>(options =>
        {
            options.RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WopiFiles");
        });
        
        // Register WOPI services
        builder.Services.AddSingleton<InMemoryFileIds>();
        builder.Services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();
        builder.Services.AddScoped<IWopiWritableStorageProvider, WopiFileSystemProvider>();
        builder.Services.AddScoped<IWopiSecurityHandler, WopiSecurityHandler>();
        builder.Services.AddScoped<IWopiLockProvider, MemoryLockProvider>();
        
        // Add WOPI
        builder.Services.AddWopi();
        
        // Add discovery
        builder.Services.AddWopiDiscovery<WopiHostOptions>(options =>
        {
            options.NetZone = NetZoneEnum.ExternalHttp;
            options.RefreshInterval = TimeSpan.FromHours(24);
        });
        
        var app = builder.Build();
        
        // Ensure root directory exists
        var rootPath = app.Services.GetRequiredService<IOptions<WopiFileSystemProviderOptions>>().Value.RootPath;
        Directory.CreateDirectory(rootPath);
        
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        
        app.Run();
    }
}
```

### 2. Shared Network Drive Integration

Integrate with shared network drives for enterprise environments:

```csharp
public class NetworkDriveWopiHost
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure for network drive
        builder.Services.Configure<WopiFileSystemProviderOptions>(options =>
        {
            options.RootPath = @"\\fileserver\shared\documents";
        });
        
        // Add custom security handler for network authentication
        builder.Services.AddScoped<IWopiSecurityHandler, NetworkDriveSecurityHandler>();
        
        // Register services
        builder.Services.AddSingleton<InMemoryFileIds>();
        builder.Services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();
        builder.Services.AddScoped<IWopiWritableStorageProvider, WopiFileSystemProvider>();
        
        // Add WOPI
        builder.Services.AddWopi();
        
        var app = builder.Build();
        app.Run();
    }
}

public class NetworkDriveSecurityHandler : IWopiSecurityHandler
{
    private readonly IUserService _userService;
    
    public async Task<WopiUserPermissions> GetUserPermissionsAsync(string userId, string resourceId)
    {
        var user = await _userService.GetUserAsync(userId);
        var filePath = DecodeFilePath(resourceId);
        
        // Check network permissions
        var hasReadAccess = await CheckNetworkFileAccess(filePath, user, FileAccess.Read);
        var hasWriteAccess = await CheckNetworkFileAccess(filePath, user, FileAccess.Write);
        
        var permissions = WopiUserPermissions.None;
        if (hasReadAccess) permissions |= WopiUserPermissions.Read;
        if (hasWriteAccess) permissions |= WopiUserPermissions.Write;
        
        return permissions;
    }
    
    private async Task<bool> CheckNetworkFileAccess(string filePath, User user, FileAccess access)
    {
        // Implement network file access checking
        // This could check Active Directory permissions, file system ACLs, etc.
        return true; // Simplified for example
    }
    
    // Implement other required methods...
}
```

### 3. Document Management System Integration

Integrate with existing document management systems:

```csharp
public class DocumentManagementWopiHost
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure file system provider
        builder.Services.Configure<WopiFileSystemProviderOptions>(options =>
        {
            options.RootPath = @"C:\DocumentManagement\Documents";
        });
        
        // Add document management services
        builder.Services.AddScoped<IDocumentService, DocumentService>();
        builder.Services.AddScoped<IVersionService, VersionService>();
        builder.Services.AddScoped<IAuditService, AuditService>();
        
        // Register WOPI services
        builder.Services.AddSingleton<InMemoryFileIds>();
        builder.Services.AddScoped<IWopiStorageProvider, DocumentManagementStorageProvider>();
        builder.Services.AddScoped<IWopiWritableStorageProvider, DocumentManagementStorageProvider>();
        builder.Services.AddScoped<IWopiSecurityHandler, DocumentManagementSecurityHandler>();
        
        // Add WOPI
        builder.Services.AddWopi();
        
        var app = builder.Build();
        app.Run();
    }
}

public class DocumentManagementStorageProvider : WopiFileSystemProvider
{
    private readonly IDocumentService _documentService;
    private readonly IVersionService _versionService;
    private readonly IAuditService _auditService;
    
    public DocumentManagementStorageProvider(
        InMemoryFileIds fileIds,
        IHostEnvironment env,
        IConfiguration configuration,
        IDocumentService documentService,
        IVersionService versionService,
        IAuditService auditService)
        : base(fileIds, env, configuration)
    {
        _documentService = documentService;
        _versionService = versionService;
        _auditService = auditService;
    }
    
    public override async Task<Stream> GetWriteStream(CancellationToken cancellationToken = default)
    {
        var stream = await base.GetWriteStream(cancellationToken);
        
        // Wrap stream to capture changes for versioning and auditing
        return new DocumentManagementStream(stream, _versionService, _auditService);
    }
    
    public override async Task<IWopiFile> CreateFileAsync(string identifier, Stream content, CancellationToken cancellationToken = default)
    {
        var file = await base.CreateFileAsync(identifier, content, cancellationToken);
        
        // Register in document management system
        await _documentService.RegisterDocumentAsync(identifier, file.Name);
        
        // Create initial version
        await _versionService.CreateVersionAsync(identifier, content);
        
        // Audit creation
        await _auditService.LogDocumentCreatedAsync(identifier, file.Name);
        
        return file;
    }
}
```

## API Reference

### WopiFileSystemProvider

Main file system storage provider implementation.

#### Constructor

```csharp
public WopiFileSystemProvider(
    InMemoryFileIds fileIds,
    IHostEnvironment env,
    IConfiguration configuration)
```

**Parameters:**
- `fileIds`: In-memory file ID storage
- `env`: Hosting environment information
- `configuration`: Application configuration

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `RootContainerPointer` | `IWopiFolder` | Reference to the root container |

#### Methods

##### GetWopiResource

```csharp
public Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
    where T : class, IWopiResource
```

Gets a WOPI resource by identifier.

**Parameters:**
- `identifier`: Base64-encoded file path identifier
- `cancellationToken`: Cancellation token

**Returns:** WOPI resource or null if not found

##### GetWopiFiles

```csharp
public IAsyncEnumerable<IWopiFile> GetWopiFiles(
    string? identifier = null,
    string? searchPattern = null,
    CancellationToken cancellationToken = default)
```

Gets files from a container.

**Parameters:**
- `identifier`: Container identifier (null for root)
- `searchPattern`: File search pattern
- `cancellationToken`: Cancellation token

**Returns:** Async enumerable of WOPI files

##### CreateFileAsync

```csharp
public Task<IWopiFile> CreateFileAsync(string identifier, Stream content, CancellationToken cancellationToken = default)
```

Creates a new file.

**Parameters:**
- `identifier`: File identifier
- `content`: File content stream
- `cancellationToken`: Cancellation token

**Returns:** Created WOPI file

### WopiFileSystemProviderOptions

Configuration options for the file system provider.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `RootPath` | `string` | Root directory path for WOPI files |

### WopiFile

File system implementation of `IWopiFile`.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Identifier` | `string` | Base64-encoded file path |
| `Name` | `string` | File name without extension |
| `Extension` | `string` | File extension without dot |
| `Exists` | `bool` | Whether file exists |
| `Length` | `long` | File size in bytes |
| `Size` | `long` | File size in bytes |
| `LastWriteTimeUtc` | `DateTime` | Last write time |
| `Version` | `string?` | File version |
| `Checksum` | `byte[]?` | File checksum |
| `Owner` | `string` | File owner |

#### Methods

##### GetReadStream

```csharp
public Task<Stream> GetReadStream(CancellationToken cancellationToken = default)
```

Gets a read-only stream for the file.

##### GetWriteStream

```csharp
public Task<Stream> GetWriteStream(CancellationToken cancellationToken = default)
```

Gets a write stream for the file.

### WopiFolder

File system implementation of `IWopiFolder`.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Identifier` | `string` | Base64-encoded folder path |
| `Name` | `string` | Folder name |

### WopiSecurityHandler

File system-based security handler.

#### Methods

##### GetUserPermissionsAsync

```csharp
public Task<WopiUserPermissions> GetUserPermissionsAsync(string userId, string resourceId)
```

Gets user permissions for a resource based on file system ACLs.

##### ValidateTokenAsync

```csharp
public Task<bool> ValidateTokenAsync(string token, string resourceId)
```

Validates JWT token for resource access.

##### GetUserIdAsync

```csharp
public Task<string> GetUserIdAsync(string token)
```

Extracts user ID from JWT token.

## Configuration

### appsettings.json

```json
{
  "Wopi": {
    "StorageOptions": {
      "RootPath": "C:\\WopiFiles"
    }
  },
  "Logging": {
    "LogLevel": {
      "WopiHost.FileSystemProvider": "Information"
    }
  }
}
```

### Environment Variables

```bash
WOPI__STORAGEOPTIONS__ROOTPATH=C:\WopiFiles
```

### Program.cs

```csharp
builder.Services.Configure<WopiFileSystemProviderOptions>(options =>
{
    options.RootPath = builder.Configuration.GetValue<string>("WOPI:StorageOptions:RootPath") 
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WopiFiles");
});
```

## Security

### File System Permissions

The file system provider respects Windows file system permissions:

```csharp
// Set file permissions programmatically
var fileInfo = new FileInfo(filePath);
var accessControl = fileInfo.GetAccessControl();
var accessRule = new FileSystemAccessRule(
    "DOMAIN\\User",
    FileSystemRights.FullControl,
    AccessControlType.Allow);
accessControl.SetAccessRule(accessRule);
fileInfo.SetAccessControl(accessControl);
```

### JWT Token Validation

Configure JWT token validation:

```csharp
builder.Services.Configure<JwtBearerOptions>(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "your-issuer",
        ValidAudience = "your-audience",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-secret-key"))
    };
});
```

## File ID Management

The file system provider uses base64-encoded file paths as identifiers:

```csharp
// Encode file path to identifier
var filePath = @"C:\WopiFiles\Documents\report.docx";
var identifier = Convert.ToBase64String(Encoding.UTF8.GetBytes(filePath));

// Decode identifier to file path
var decodedPath = Encoding.UTF8.GetString(Convert.FromBase64String(identifier));
```

## Examples

### Custom File Operations

```csharp
public class CustomFileOperations
{
    private readonly WopiFileSystemProvider _storageProvider;
    
    public async Task<string> CreateDocumentAsync(string fileName, string content)
    {
        var identifier = GenerateFileIdentifier(fileName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        
        var file = await _storageProvider.CreateFileAsync(identifier, stream);
        return file.Identifier;
    }
    
    public async Task<byte[]> GetFileContentAsync(string identifier)
    {
        var file = await _storageProvider.GetWopiResource<IWopiFile>(identifier);
        if (file == null)
        {
            throw new FileNotFoundException($"File with identifier {identifier} not found");
        }
        
        using var stream = await file.GetReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
    
    private string GenerateFileIdentifier(string fileName)
    {
        var filePath = Path.Combine("Documents", fileName);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(filePath));
    }
}
```

### File Monitoring

```csharp
public class FileMonitorService : BackgroundService
{
    private readonly WopiFileSystemProvider _storageProvider;
    private readonly FileSystemWatcher _watcher;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rootPath = GetRootPath();
        _watcher = new FileSystemWatcher(rootPath)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileCreated;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileRenamed;
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
    
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Handle file changes
        Console.WriteLine($"File changed: {e.FullPath}");
    }
    
    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        // Handle file creation
        Console.WriteLine($"File created: {e.FullPath}");
    }
    
    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        // Handle file deletion
        Console.WriteLine($"File deleted: {e.FullPath}");
    }
    
    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Handle file rename
        Console.WriteLine($"File renamed: {e.OldFullPath} -> {e.FullPath}");
    }
}
```

## Dependencies

- `WopiHost.Abstractions`: Core WOPI interfaces
- `System.IO.FileSystem.AccessControl`: Windows ACL support
- `Microsoft.Extensions.Hosting.Abstractions`: Hosting environment
- `Microsoft.AspNetCore.Authorization`: Authorization support
- `Microsoft.Extensions.Configuration.Abstractions`: Configuration support
- `Microsoft.Extensions.Configuration.Binder`: Configuration binding
- `System.IdentityModel.Tokens.Jwt`: JWT token handling

## Performance Considerations

- **File ID Caching**: Uses in-memory caching for file ID to path mapping
- **Stream Handling**: Efficient stream operations for large files
- **Concurrent Access**: Thread-safe file operations
- **Memory Management**: Proper disposal of streams and resources

## Troubleshooting

### Common Issues

1. **File Not Found**: Ensure the root path exists and is accessible
2. **Permission Denied**: Check file system permissions
3. **Invalid File ID**: Verify base64 encoding/decoding
4. **Lock Issues**: Check file locking implementation

### Debugging

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "WopiHost.FileSystemProvider": "Debug"
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
