# WopiHost.Abstractions

[![NuGet](https://img.shields.io/nuget/v/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions)
[![NuGet](https://img.shields.io/nuget/dt/WopiHost.Abstractions.svg)](https://www.nuget.org/packages/WopiHost.Abstractions)

A .NET library containing the core abstractions and interfaces for building WOPI (Web Application Open Platform Interface) host implementations. This package defines the contracts that WOPI hosts must implement to integrate with Office Online Server.

## Features

- **Core Interfaces**: Essential interfaces for WOPI host implementation
- **Storage Abstractions**: Abstract file and folder operations
- **Security Contracts**: Authentication and authorization interfaces
- **WOPI Models**: Standard WOPI data models and enums
- **Lock Management**: File locking and concurrency control interfaces
- **Host Capabilities**: WOPI host capability definitions

## Installation

```bash
dotnet add package WopiHost.Abstractions
```

## Quick Start

### Basic Interface Usage

```csharp
using WopiHost.Abstractions;

// Implement a custom storage provider
public class CustomStorageProvider : IWopiStorageProvider
{
    public Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        // Your implementation here
        throw new NotImplementedException();
    }
    
    public IAsyncEnumerable<IWopiFile> GetWopiFiles(string? identifier = null, string? searchPattern = null, CancellationToken cancellationToken = default)
    {
        // Your implementation here
        throw new NotImplementedException();
    }
    
    public IAsyncEnumerable<IWopiFolder> GetWopiContainers(string? identifier = null, CancellationToken cancellationToken = default)
    {
        // Your implementation here
        throw new NotImplementedException();
    }
    
    public IWopiFolder RootContainerPointer { get; } = new CustomFolder("root", "root-id");
}
```

## Hero Scenarios

### 1. Custom Cloud Storage Integration

Integrate with any cloud storage provider (Azure Blob, AWS S3, Google Cloud, etc.):

```csharp
public class AzureBlobStorageProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    
    public AzureBlobStorageProvider(BlobServiceClient blobServiceClient, string containerName)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = containerName;
    }
    
    public async Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(identifier);
        
        if (typeof(T) == typeof(IWopiFile))
        {
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (exists.Value)
            {
                return new AzureBlobFile(blobClient, identifier) as T;
            }
        }
        else if (typeof(T) == typeof(IWopiFolder))
        {
            // Handle folder logic
            return new AzureBlobFolder(identifier) as T;
        }
        
        return null;
    }
    
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(string? identifier = null, string? searchPattern = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var prefix = identifier ?? "";
        
        await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            if (blobItem.Properties.ContentType?.StartsWith("application/") == true)
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                yield return new AzureBlobFile(blobClient, blobItem.Name);
            }
        }
    }
    
    // Implement other required methods...
}

public class AzureBlobFile : IWopiFile
{
    private readonly BlobClient _blobClient;
    
    public AzureBlobFile(BlobClient blobClient, string identifier)
    {
        _blobClient = blobClient;
        Identifier = identifier;
    }
    
    public string Identifier { get; }
    public string Name => Path.GetFileNameWithoutExtension(_blobClient.Name);
    public string Extension => Path.GetExtension(_blobClient.Name).TrimStart('.');
    public bool Exists => true; // We know it exists if we got here
    public long Length => 0; // Would need to fetch properties
    public long Size => Length;
    public DateTime LastWriteTimeUtc => DateTime.UtcNow; // Would need to fetch properties
    public string? Version => null;
    public byte[]? Checksum => null;
    public string Owner => "system"; // Would need to fetch metadata
    
    public async Task<Stream> GetReadStream(CancellationToken cancellationToken = default)
    {
        var response = await _blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }
    
    public async Task<Stream> GetWriteStream(CancellationToken cancellationToken = default)
    {
        // Implementation for write stream
        throw new NotImplementedException();
    }
}
```

### 2. Database-Backed Document Storage

Store documents in a database with metadata:

```csharp
public class DatabaseStorageProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private readonly ApplicationDbContext _context;
    private readonly IBlobStorage _blobStorage;
    
    public async Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == identifier, cancellationToken);
            
        if (document == null) return null;
        
        if (typeof(T) == typeof(IWopiFile))
        {
            return new DatabaseFile(document, _blobStorage) as T;
        }
        else if (typeof(T) == typeof(IWopiFolder))
        {
            return new DatabaseFolder(document) as T;
        }
        
        return null;
    }
    
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(string? identifier = null, string? searchPattern = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _context.Documents.AsQueryable();
        
        if (!string.IsNullOrEmpty(identifier))
        {
            query = query.Where(d => d.ParentId == identifier);
        }
        
        if (!string.IsNullOrEmpty(searchPattern))
        {
            query = query.Where(d => EF.Functions.Like(d.Name, searchPattern));
        }
        
        await foreach (var document in query.AsAsyncEnumerable())
        {
            yield return new DatabaseFile(document, _blobStorage);
        }
    }
    
    // Implement other methods...
}

public class DatabaseFile : IWopiFile
{
    private readonly Document _document;
    private readonly IBlobStorage _blobStorage;
    
    public DatabaseFile(Document document, IBlobStorage blobStorage)
    {
        _document = document;
        _blobStorage = blobStorage;
    }
    
    public string Identifier => _document.Id;
    public string Name => _document.Name;
    public string Extension => _document.Extension;
    public bool Exists => true;
    public long Length => _document.Size;
    public long Size => _document.Size;
    public DateTime LastWriteTimeUtc => _document.LastModified;
    public string? Version => _document.Version;
    public byte[]? Checksum => _document.Checksum;
    public string Owner => _document.OwnerId;
    
    public async Task<Stream> GetReadStream(CancellationToken cancellationToken = default)
    {
        return await _blobStorage.GetStreamAsync(_document.BlobPath, cancellationToken);
    }
    
    public async Task<Stream> GetWriteStream(CancellationToken cancellationToken = default)
    {
        return await _blobStorage.GetWriteStreamAsync(_document.BlobPath, cancellationToken);
    }
}
```

### 3. Custom Security Implementation

Implement custom authentication and authorization:

```csharp
public class CustomSecurityHandler : IWopiSecurityHandler
{
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;
    
    public async Task<WopiUserPermissions> GetUserPermissionsAsync(string userId, string resourceId)
    {
        var user = await _userService.GetUserAsync(userId);
        var resource = await GetResourceAsync(resourceId);
        
        var permissions = WopiUserPermissions.None;
        
        if (user.CanRead(resource))
        {
            permissions |= WopiUserPermissions.Read;
        }
        
        if (user.CanWrite(resource))
        {
            permissions |= WopiUserPermissions.Write;
        }
        
        if (user.CanDelete(resource))
        {
            permissions |= WopiUserPermissions.Delete;
        }
        
        return permissions;
    }
    
    public async Task<bool> ValidateTokenAsync(string token, string resourceId)
    {
        try
        {
            var claims = _tokenService.ValidateToken(token);
            var userId = claims.FindFirst("user_id")?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }
            
            var permissions = await GetUserPermissionsAsync(userId, resourceId);
            return permissions != WopiUserPermissions.None;
        }
        catch
        {
            return false;
        }
    }
    
    // Implement other required methods...
}

public class CustomLockProvider : IWopiLockProvider
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CustomLockProvider> _logger;
    
    public async Task<bool> LockAsync(string resourceId, string lockId, TimeSpan timeout)
    {
        try
        {
            var lockKey = $"wopi:lock:{resourceId}";
            var existingLock = await _cache.GetStringAsync(lockKey);
            
            if (!string.IsNullOrEmpty(existingLock) && existingLock != lockId)
            {
                return false; // Resource is already locked by someone else
            }
            
            await _cache.SetStringAsync(lockKey, lockId, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = timeout
            });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire lock for resource {ResourceId}", resourceId);
            return false;
        }
    }
    
    public async Task<bool> UnlockAsync(string resourceId, string lockId)
    {
        try
        {
            var lockKey = $"wopi:lock:{resourceId}";
            var existingLock = await _cache.GetStringAsync(lockKey);
            
            if (existingLock != lockId)
            {
                return false; // Lock doesn't match
            }
            
            await _cache.RemoveAsync(lockKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release lock for resource {ResourceId}", resourceId);
            return false;
        }
    }
    
    // Implement other required methods...
}
```

## Core Interfaces

### IWopiStorageProvider

Base interface for read-only storage operations.

```csharp
public interface IWopiStorageProvider
{
    Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource;
    
    IAsyncEnumerable<IWopiFile> GetWopiFiles(string? identifier = null, string? searchPattern = null, CancellationToken cancellationToken = default);
    
    IAsyncEnumerable<IWopiFolder> GetWopiContainers(string? identifier = null, CancellationToken cancellationToken = default);
    
    IWopiFolder RootContainerPointer { get; }
    
    Task<IEnumerable<IWopiResource>> GetAncestorsAsync(string identifier, CancellationToken cancellationToken = default);
}
```

### IWopiWritableStorageProvider

Extends `IWopiStorageProvider` with write operations.

```csharp
public interface IWopiWritableStorageProvider : IWopiStorageProvider
{
    Task<IWopiFile> CreateFileAsync(string identifier, Stream content, CancellationToken cancellationToken = default);
    
    Task<IWopiFolder> CreateFolderAsync(string identifier, CancellationToken cancellationToken = default);
    
    Task DeleteFileAsync(string identifier, CancellationToken cancellationToken = default);
    
    Task DeleteFolderAsync(string identifier, CancellationToken cancellationToken = default);
    
    Task<IWopiFile> RenameFileAsync(string identifier, string newName, CancellationToken cancellationToken = default);
    
    Task<IWopiFolder> RenameFolderAsync(string identifier, string newName, CancellationToken cancellationToken = default);
}
```

### IWopiFile

Represents a file in the WOPI system.

```csharp
public interface IWopiFile : IWopiResource
{
    string Owner { get; }
    bool Exists { get; }
    long Length { get; }
    DateTime LastWriteTimeUtc { get; }
    string Extension { get; }
    string? Version { get; }
    byte[]? Checksum { get; }
    long Size { get; }
    
    Task<Stream> GetReadStream(CancellationToken cancellationToken = default);
    Task<Stream> GetWriteStream(CancellationToken cancellationToken = default);
}
```

### IWopiFolder

Represents a folder/container in the WOPI system.

```csharp
public interface IWopiFolder : IWopiResource
{
    // Inherits Name and Identifier from IWopiResource
}
```

### IWopiSecurityHandler

Handles authentication and authorization.

```csharp
public interface IWopiSecurityHandler
{
    Task<WopiUserPermissions> GetUserPermissionsAsync(string userId, string resourceId);
    Task<bool> ValidateTokenAsync(string token, string resourceId);
    Task<string> GetUserIdAsync(string token);
}
```

### IWopiLockProvider

Manages file locking for concurrent access.

```csharp
public interface IWopiLockProvider
{
    Task<bool> LockAsync(string resourceId, string lockId, TimeSpan timeout);
    Task<bool> UnlockAsync(string resourceId, string lockId);
    Task<string?> GetLockAsync(string resourceId);
    Task<bool> RefreshLockAsync(string resourceId, string lockId, TimeSpan timeout);
}
```

## WOPI Models

### WopiCheckFileInfo

Standard WOPI file information model.

```csharp
public class WopiCheckFileInfo
{
    public string BaseFileName { get; set; }
    public string OwnerId { get; set; }
    public long Size { get; set; }
    public string UserId { get; set; }
    public string Version { get; set; }
    public string Sha256 { get; set; }
    public bool UserCanWrite { get; set; }
    public bool UserCanNotWriteRelative { get; set; }
    public bool UserCanRename { get; set; }
    public bool UserCanAttend { get; set; }
    public bool UserCanPresent { get; set; }
    public bool UserCanEdit { get; set; }
    public bool UserCanView { get; set; }
    public bool UserCanDelete { get; set; }
    // ... more properties
}
```

### WopiHostCapabilities

Defines what the WOPI host supports.

```csharp
public class WopiHostCapabilities : IWopiHostCapabilities
{
    public bool SupportsCoauth { get; set; }
    public bool SupportsCobalt { get; set; }
    public bool SupportsFolders { get; set; } = true;
    public bool SupportsContainers { get; set; } = true;
    public bool SupportsLocks { get; set; }
    public bool SupportsGetLock { get; set; }
    public bool SupportsExtendedLockLength { get; set; } = true;
    public bool SupportsEcosystem { get; set; } = true;
    public bool SupportsGetFileWopiSrc { get; set; }
    public IEnumerable<string> SupportedShareUrlTypes { get; set; } = [];
    public bool SupportsScenarioLinks { get; set; }
    public bool SupportsSecureStore { get; set; }
    public bool SupportsFileCreation { get; set; }
    public bool SupportsUpdate { get; set; } = true;
    public bool SupportsRename { get; set; } = true;
    public bool SupportsDeleteFile { get; set; } = true;
    public bool SupportsUserInfo { get; set; } = true;
}
```

## Enums

### PermissionEnum

File permission levels.

```csharp
public enum PermissionEnum
{
    None = 0,
    Read = 1,
    Write = 2,
    Delete = 4,
    All = Read | Write | Delete
}
```

### WopiFileOperations

Standard WOPI file operations.

```csharp
public enum WopiFileOperations
{
    GetFile,
    PutFile,
    Lock,
    Unlock,
    RefreshLock,
    GetLock,
    CheckFileInfo,
    PutRelativeFile,
    DeleteFile,
    RenameFile,
    PutUserInfo,
    ReadSecureStore,
    GetRestrictedLink,
    RevokeRestrictedLink,
    CheckContainerInfo,
    GetContainer,
    CreateContainer,
    DeleteContainer,
    CheckEcosystem,
    GetEcosystem,
    GetFileWopiSrc,
    EnumerateChildren,
    CheckFolderInfo,
    PutFileWopiSrc,
    DeleteFileWopiSrc,
    PutRelativeFileLocal,
    GetFileWopiSrc,
    PutFileWopiSrc,
    DeleteFileWopiSrc,
    PutRelativeFileLocal,
    GetFileWopiSrc,
    PutFileWopiSrc,
    DeleteFileWopiSrc,
    PutRelativeFileLocal
}
```

## Dependencies

- `Microsoft.AspNetCore.Authorization`: For authorization support
- `Microsoft.IdentityModel.Tokens`: For JWT token handling

## Examples

### Custom Resource Implementation

```csharp
public class CustomFile : IWopiFile
{
    public CustomFile(string path, string identifier)
    {
        Path = path;
        Identifier = identifier;
        var fileInfo = new FileInfo(path);
        Name = fileInfo.Name;
        Exists = fileInfo.Exists;
        Length = fileInfo.Length;
        LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        Extension = fileInfo.Extension.TrimStart('.');
        Owner = Environment.UserName;
    }
    
    public string Path { get; }
    public string Identifier { get; }
    public string Name { get; }
    public bool Exists { get; }
    public long Length { get; }
    public long Size => Length;
    public DateTime LastWriteTimeUtc { get; }
    public string Extension { get; }
    public string? Version => null;
    public byte[]? Checksum => null;
    public string Owner { get; }
    
    public Task<Stream> GetReadStream(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(File.OpenRead(Path));
    }
    
    public Task<Stream> GetWriteStream(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(File.OpenWrite(Path));
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
