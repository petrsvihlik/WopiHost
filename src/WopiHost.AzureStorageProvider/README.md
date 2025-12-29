# WopiHost.AzureStorageProvider

[![NuGet](https://img.shields.io/nuget/v/WopiHost.AzureStorageProvider.svg)](https://www.nuget.org/packages/WopiHost.AzureStorageProvider)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.AzureStorageProvider.svg)](https://www.nuget.org/packages/WopiHost.AzureStorageProvider)

A .NET library providing an Azure Blob Storage-based implementation of WOPI (Web Application Open Platform Interface) storage providers. This package implements `IWopiStorageProvider` and `IWopiWritableStorageProvider` using Azure Blob Storage, enabling scalable cloud-based document storage for WOPI host applications.

## Features

- **Azure Blob Storage Integration**: Complete Azure Blob Storage-based WOPI storage implementation
- **Read/Write Operations**: Full support for both read and write operations
- **Scalable Storage**: Leverage Azure's scalable blob storage for document management
- **Security Integration**: JWT-based authentication and authorization
- **Base64 Encoding**: Secure file identifier encoding for blob paths
- **In-Memory Caching**: Efficient file ID to blob path mapping
- **Managed Identity Support**: Support for Azure Managed Identity authentication
- **Container Management**: Automatic container creation and management

## Installation

```bash
dotnet add package WopiHost.AzureStorageProvider
```

## Quick Start

### Basic Setup

```csharp
using WopiHost.AzureStorageProvider;
using WopiHost.Core.Extensions;

// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure Azure Storage provider
builder.Services.Configure<WopiAzureStorageProviderOptions>(options =>
{
    options.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=...";
    options.ContainerName = "wopi-files";
    options.RootPath = "documents"; // Optional subfolder
});

// Register services
builder.Services.AddSingleton<AzureFileIds>();
builder.Services.AddScoped<IWopiStorageProvider, WopiAzureStorageProvider>();
builder.Services.AddScoped<IWopiWritableStorageProvider, WopiAzureStorageProvider>();
builder.Services.AddScoped<IWopiSecurityHandler, WopiAzureSecurityHandler>();

// Add WOPI
builder.Services.AddWopi();

var app = builder.Build();
app.Run();
```

### Configuration

```json
{
  "WopiHost": {
    "StorageOptions": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...",
      "ContainerName": "wopi-files",
      "RootPath": "documents",
      "UseManagedIdentity": false,
      "CreateContainerIfNotExists": true,
      "FileNameMaxLength": 250
    }
  }
}
```

### Using Managed Identity

```csharp
builder.Services.Configure<WopiAzureStorageProviderOptions>(options =>
{
    options.UseManagedIdentity = true;
    options.AccountName = "myaccount";
    options.ContainerName = "wopi-files";
});
```

## Configuration Options

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `ConnectionString` | string | Yes* | Azure Storage connection string |
| `AccountName` | string | Yes* | Azure Storage account name |
| `AccountKey` | string | No | Azure Storage account key |
| `ContainerName` | string | Yes | Name of the blob container |
| `RootPath` | string | No | Optional root path within the container |
| `UseManagedIdentity` | bool | No | Use Azure Managed Identity (default: false) |
| `CreateContainerIfNotExists` | bool | No | Create container if it doesn't exist (default: true) |
| `FileNameMaxLength` | int | No | Maximum file name length (default: 250) |
| `ContainerPublicAccess` | PublicAccessType | No | Container public access level (default: None) |

*Either `ConnectionString` or `AccountName` (with `AccountKey` or `UseManagedIdentity=true`) is required.

## Architecture

### File Mapping
- **Files** → Azure Blobs
- **Folders** → Azure Blob Containers (represented by placeholder blobs)
- **File IDs** → Base64-encoded blob paths
- **Root Container** → Main Azure Storage Container

### Security
The provider supports multiple authentication methods:
- **Connection String**: Traditional account key authentication
- **Account Key**: Direct account name and key authentication
- **Managed Identity**: Azure Managed Identity for secure, keyless authentication

### Performance Considerations
- **Connection Pooling**: Uses Azure SDK's built-in connection pooling
- **Async Operations**: All operations are fully asynchronous
- **Caching**: In-memory file ID to blob path mapping
- **Retry Policies**: Built-in retry logic for transient failures

## Error Handling

The provider maps Azure Storage exceptions to appropriate WOPI errors:
- `BlobNotFoundException` → `FileNotFoundException`
- `ContainerNotFoundException` → `DirectoryNotFoundException`
- `RequestFailedException` → Appropriate HTTP status codes

## Examples

### Basic File Operations

```csharp
// Get a file
var file = await storageProvider.GetWopiResource<IWopiFile>("file-id");

// Read file content
using var stream = await file.GetReadStream();

// Write file content
using var writeStream = await file.GetWriteStream();
await writeStream.WriteAsync(content);
```

### Container Operations

```csharp
// List files in a container
await foreach (var file in storageProvider.GetWopiFiles("container-id"))
{
    Console.WriteLine($"File: {file.Name}");
}

// List containers
await foreach (var container in storageProvider.GetWopiContainers())
{
    Console.WriteLine($"Container: {container.Name}");
}
```

### Write Operations

```csharp
// Create a new file
var newFile = await writableStorageProvider.CreateWopiChildResource<IWopiFile>(
    "container-id", "new-file.docx");

// Delete a file
await writableStorageProvider.DeleteWopiResource<IWopiFile>("file-id");

// Rename a file
await writableStorageProvider.RenameWopiResource<IWopiFile>(
    "file-id", "new-name.docx");
```

## Dependencies

- Azure.Storage.Blobs
- Azure.Identity
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.Hosting.Abstractions
- Microsoft.Extensions.Logging.Abstractions
- System.IdentityModel.Tokens.Jwt

## License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE.txt) file for details.

## Contributing

Contributions are welcome! Please read our [Contributing Guide](../../CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Support

For support and questions, please open an issue on the [GitHub repository](https://github.com/petrsvihlik/WopiHost/issues).
