using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Azure Blob Storage implementation of WOPI storage providers.
/// </summary>
public class WopiAzureStorageProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private readonly AzureFileIds _fileIds;
    private readonly BlobContainerClient _containerClient;
    private readonly WopiAzureStorageProviderOptions _options;
    private readonly ILogger<WopiAzureStorageProvider> _logger;

    /// <summary>
    /// Reference to the root container.
    /// </summary>
    public IWopiFolder RootContainerPointer { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiAzureStorageProvider"/>.
    /// </summary>
    /// <param name="fileIds">Azure file ID management</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger instance</param>
    public WopiAzureStorageProvider(
        AzureFileIds fileIds,
        IConfiguration configuration,
        ILogger<WopiAzureStorageProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _fileIds = fileIds ?? throw new ArgumentNullException(nameof(fileIds));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _options = configuration.GetSection(WopiConfigurationSections.STORAGE_OPTIONS)?
            .Get<WopiAzureStorageProviderOptions>() ?? throw new ArgumentNullException(nameof(configuration));

        ValidateOptions();

        _containerClient = CreateBlobServiceClient();

        // Ensure container exists
        if (_options.CreateContainerIfNotExists)
        {
            _containerClient.CreateIfNotExistsAsync(_options.ContainerPublicAccess).Wait();
        }

        // Scan existing blobs
        if (!_fileIds.WasScanned)
        {
            _fileIds.ScanAllAsync(_containerClient, _options.RootPath).Wait();
        }

        // Set up root container
        var rootId = _fileIds.GetPath(_options.RootPath ?? string.Empty) ?? 
                    _fileIds.AddFile(_options.RootPath ?? string.Empty);
        RootContainerPointer = new WopiAzureFolder(_options.ContainerName, rootId, _options.RootPath);
    }

    /// <inheritdoc/>
    public Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (!_fileIds.TryGetPath(identifier, out var blobPath))
        {
            return Task.FromResult<T?>(null);
        }

        return typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => GetWopiFile(blobPath, identifier, cancellationToken).ContinueWith(t => t.Result as T),
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => GetWopiFolder(blobPath, identifier, cancellationToken).ContinueWith(t => t.Result as T),
            _ => throw new NotSupportedException($"Unsupported resource type: {typeof(T).Name}"),
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string? identifier = null,
        string? searchPattern = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var folderPath = _fileIds.GetPath(identifier ?? RootContainerPointer.Identifier) ?? 
                        throw new DirectoryNotFoundException($"Directory '{identifier}' not found");

        var prefix = string.IsNullOrEmpty(folderPath) ? null : folderPath.TrimEnd('/') + "/";
        
        if (!string.IsNullOrEmpty(searchPattern))
        {
            // Convert search pattern to Azure blob pattern
            var pattern = searchPattern.Replace("*", "").Replace("?", "");
            prefix = prefix + pattern;
        }

        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            if (blobItem.Properties.BlobType == Azure.Storage.Blobs.Models.BlobType.Block)
            {
                var fileId = _fileIds.TryGetFileId(blobItem.Name, out var id) ? id : _fileIds.AddFile(blobItem.Name);
                var result = await GetWopiFile(blobItem.Name, fileId, cancellationToken);
                if (result != null)
                {
                    yield return result;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFolder> GetWopiContainers(
        string? identifier = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var folderPath = _fileIds.GetPath(identifier ?? RootContainerPointer.Identifier) ?? 
                        throw new DirectoryNotFoundException($"Directory '{identifier}' not found");

        var prefix = string.IsNullOrEmpty(folderPath) ? null : folderPath.TrimEnd('/') + "/";
        var folders = new HashSet<string>();

        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            if (blobItem.Properties.BlobType == Azure.Storage.Blobs.Models.BlobType.Block)
            {
                var relativePath = blobItem.Name;
                if (!string.IsNullOrEmpty(prefix))
                {
                    relativePath = blobItem.Name.Substring(prefix.Length);
                }

                var pathParts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathParts.Length > 1)
                {
                    var folderName = pathParts[0];
                    var fullFolderPath = string.IsNullOrEmpty(folderPath) ? folderName : $"{folderPath.TrimEnd('/')}/{folderName}";
                    
                    if (folders.Add(fullFolderPath))
                    {
                        var folderId = _fileIds.TryGetFileId(fullFolderPath, out var id) ? id : _fileIds.AddFile(fullFolderPath);
                        yield return new WopiAzureFolder(_options.ContainerName, folderId, fullFolderPath);
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiFolder>> GetAncestors<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        var result = new List<IWopiFolder>();
        
        if (typeof(T) == typeof(IWopiFile))
        {
            identifier = GetFileParentIdentifier(identifier);
        }

        var container = await GetWopiResource<IWopiFolder>(identifier, cancellationToken)
            ?? throw new DirectoryNotFoundException($"Directory '{identifier}' not found.");

        if (container.Identifier == RootContainerPointer.Identifier && typeof(T) == typeof(IWopiFile))
        {
            result.Add(container);
        }

        while (container.Identifier != RootContainerPointer.Identifier)
        {
            var parentId = GetFolderParentIdentifier(container.Identifier);
            container = await GetWopiResource<IWopiFolder>(parentId, cancellationToken)
                ?? throw new DirectoryNotFoundException($"Directory '{parentId}' not found.");
            result.Add(container);
        }

        result.Reverse();
        return result.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<T?> GetWopiResourceByName<T>(
        string containerId,
        string name,
        CancellationToken cancellationToken = default) where T : class, IWopiResource
    {
        if (!_fileIds.TryGetPath(containerId, out var dirPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }

        var searchPath = string.IsNullOrEmpty(dirPath) ? name : $"{dirPath.TrimEnd('/')}/{name}";
        
        if (!_fileIds.TryGetFileId(searchPath, out var nameId))
        {
            return default;
        }

        T? result = typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => await GetWopiFile(searchPath, nameId, cancellationToken) as T,
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => await GetWopiFolder(searchPath, nameId, cancellationToken) as T,
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
        return result;
    }

    #region IWopiWritableStorageProvider

    /// <inheritdoc/>
    public int FileNameMaxLength => _options.FileNameMaxLength;

    /// <inheritdoc/>
    public Task<bool> CheckValidName<T>(
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        return typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => 
                Task.FromResult(name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.Length < FileNameMaxLength),
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => 
                Task.FromResult(name.IndexOfAny(Path.GetInvalidPathChars()) < 0),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
    }

    /// <inheritdoc/>
    public async Task<string> GetSuggestedName<T>(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (!await CheckValidName<T>(name, cancellationToken))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(name));
        }

        if (!_fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }

        var newPath = string.IsNullOrEmpty(fullPath) ? name : $"{fullPath.TrimEnd('/')}/{name}";

        if (typeof(T) == typeof(IWopiFolder))
        {
            if (await BlobExistsAsync(newPath))
            {
                var newName = name;
                var counter = 1;
                while (await BlobExistsAsync($"{fullPath.TrimEnd('/')}/{newName}"))
                {
                    newName = $"{name} ({counter++})";
                }
                return newName;
            }
            else
            {
                return name;
            }
        }
        else if (typeof(T) == typeof(IWopiFile))
        {
            if (await BlobExistsAsync(newPath))
            {
                var newName = name;
                var counter = 1;
                var extension = Path.GetExtension(name);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
                while (await BlobExistsAsync($"{fullPath.TrimEnd('/')}/{newName}"))
                {
                    newName = $"{nameWithoutExt} ({counter++}){extension}";
                }
                return newName;
            }
            else
            {
                return name;
            }
        }
        else
        {
            throw new NotSupportedException("Unsupported resource type.");
        }
    }

    /// <inheritdoc/>
    public async Task<T?> CreateWopiChildResource<T>(
        string? containerId,
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        return typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => 
                await CreateWopiFile(containerId ?? RootContainerPointer.Identifier, name, cancellationToken) as T,
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => 
                await CreateWopiChildContainer(containerId ?? RootContainerPointer.Identifier, name, cancellationToken) as T,
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        return typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => 
                await DeleteWopiFile(identifier, cancellationToken),
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => 
                await DeleteWopiContainer(identifier, cancellationToken),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
    }

    /// <inheritdoc/>
    public async Task<bool> RenameWopiResource<T>(string identifier, string requestedName, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (!await CheckValidName<T>(requestedName, cancellationToken))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(requestedName));
        }

        if (!_fileIds.TryGetPath(identifier, out var fullPath))
        {
            throw new FileNotFoundException($"Resource '{identifier}' not found.");
        }

        var parentPath = Path.GetDirectoryName(fullPath)?.Replace('\\', '/') ?? "";
        var newPath = string.IsNullOrEmpty(parentPath) ? requestedName : $"{parentPath}/{requestedName}";

        if (typeof(T) == typeof(IWopiFile))
        {
            if (await BlobExistsAsync(newPath))
            {
                throw new InvalidOperationException($"Target file '{newPath}' already exists.");
            }

            var sourceBlob = _containerClient.GetBlobClient(fullPath);
            var destBlob = _containerClient.GetBlobClient(newPath);
            
            await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken);
            await sourceBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            
            _fileIds.UpdateFile(identifier, newPath);
        }
        else if (typeof(T) == typeof(IWopiFolder))
        {
            if (await BlobExistsAsync(newPath))
            {
                throw new InvalidOperationException($"Target directory '{newPath}' already exists.");
            }

            // For folders, we need to rename all blobs with the folder prefix
            var prefix = fullPath.TrimEnd('/') + "/";
            var newPrefix = newPath.TrimEnd('/') + "/";

            await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                var relativePath = blobItem.Name.Substring(prefix.Length);
                var newBlobPath = newPrefix + relativePath;
                
                var sourceBlob = _containerClient.GetBlobClient(blobItem.Name);
                var destBlob = _containerClient.GetBlobClient(newBlobPath);
                
                await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken);
                await sourceBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }

            _fileIds.UpdateFile(identifier, newPath);
        }
        else
        {
            throw new NotSupportedException("Unsupported resource type.");
        }

        return true;
    }

    #endregion

    #region Private Methods

    private void ValidateOptions()
    {
        if (string.IsNullOrEmpty(_options.ContainerName))
        {
            throw new ArgumentException("ContainerName is required.", nameof(_options.ContainerName));
        }

        if (!_options.UseManagedIdentity)
        {
            if (string.IsNullOrEmpty(_options.ConnectionString) && 
                (string.IsNullOrEmpty(_options.AccountName) || string.IsNullOrEmpty(_options.AccountKey)))
            {
                throw new ArgumentException("Either ConnectionString or AccountName/AccountKey must be provided when not using managed identity.");
            }
        }
        else
        {
            if (string.IsNullOrEmpty(_options.AccountName))
            {
                throw new ArgumentException("AccountName is required when using managed identity.");
            }
        }
    }

    private BlobContainerClient CreateBlobServiceClient()
    {
        if (_options.UseManagedIdentity)
        {
            var credential = new Azure.Identity.DefaultAzureCredential();
            var serviceUri = new Uri($"https://{_options.AccountName}.blob.core.windows.net");
            var serviceClient = new BlobServiceClient(serviceUri, credential);
            return serviceClient.GetBlobContainerClient(_options.ContainerName);
        }
        else if (!string.IsNullOrEmpty(_options.ConnectionString))
        {
            var serviceClient = new BlobServiceClient(_options.ConnectionString);
            return serviceClient.GetBlobContainerClient(_options.ContainerName);
        }
        else
        {
            var serviceUri = new Uri($"https://{_options.AccountName}.blob.core.windows.net");
            var credential = new Azure.Storage.StorageSharedKeyCredential(_options.AccountName, _options.AccountKey!);
            var serviceClient = new BlobServiceClient(serviceUri, credential);
            return serviceClient.GetBlobContainerClient(_options.ContainerName);
        }
    }

    private Task<IWopiFile?> GetWopiFile(string blobPath, string fileId, CancellationToken cancellationToken)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobPath);
            return Task.FromResult<IWopiFile?>(new WopiAzureFile(blobClient, fileId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file {BlobPath}", blobPath);
            return Task.FromResult<IWopiFile?>(null);
        }
    }

    private Task<IWopiFolder?> GetWopiFolder(string blobPath, string folderId, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult<IWopiFolder?>(new WopiAzureFolder(_options.ContainerName, folderId, blobPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting folder {BlobPath}", blobPath);
            return Task.FromResult<IWopiFolder?>(null);
        }
    }

    private async Task<bool> BlobExistsAsync(string blobPath)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobPath);
            var response = await blobClient.ExistsAsync();
            return response.Value;
        }
        catch
        {
            return false;
        }
    }

    private async Task<IWopiFile?> CreateWopiFile(string containerId, string name, CancellationToken cancellationToken)
    {
        if (!_fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }

        var newPath = string.IsNullOrEmpty(fullPath) ? name : $"{fullPath.TrimEnd('/')}/{name}";
        
        if (await BlobExistsAsync(newPath))
        {
            throw new ArgumentException($"File '{newPath}' already exists.", nameof(name));
        }

        // Create an empty blob
        var blobClient = _containerClient.GetBlobClient(newPath);
        await blobClient.UploadAsync(new MemoryStream(), overwrite: false, cancellationToken);

        var newFileId = _fileIds.AddFile(newPath);
        return await GetWopiFile(newPath, newFileId, cancellationToken);
    }

    private async Task<IWopiFolder?> CreateWopiChildContainer(string containerId, string name, CancellationToken cancellationToken)
    {
        if (!_fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }

        var newPath = string.IsNullOrEmpty(fullPath) ? name : $"{fullPath.TrimEnd('/')}/{name}";
        
        if (await BlobExistsAsync(newPath))
        {
            throw new ArgumentException($"Directory '{newPath}' already exists.", nameof(name));
        }

        // Create a placeholder blob to represent the folder
        var folderBlobPath = newPath.TrimEnd('/') + "/.folder";
        var blobClient = _containerClient.GetBlobClient(folderBlobPath);
        await blobClient.UploadAsync(new MemoryStream(), overwrite: false, cancellationToken);

        var newId = _fileIds.AddFile(newPath);
        return await GetWopiFolder(newPath, newId, cancellationToken);
    }

    private async Task<bool> DeleteWopiFile(string identifier, CancellationToken cancellationToken)
    {
        if (!_fileIds.TryGetPath(identifier, out var fullPath))
        {
            throw new FileNotFoundException($"File '{identifier}' not found.");
        }

        var blobClient = _containerClient.GetBlobClient(fullPath);
        var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        
        if (response.Value)
        {
            _fileIds.RemoveId(identifier);
        }
        
        return response.Value;
    }

    private async Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken)
    {
        if (!_fileIds.TryGetPath(identifier, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{identifier}' not found.");
        }

        var prefix = fullPath.TrimEnd('/') + "/";
        var deleted = false;

        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            if (response.Value)
            {
                deleted = true;
            }
        }

        if (deleted)
        {
            _fileIds.RemoveId(identifier);
        }

        return deleted;
    }

    private string GetFileParentIdentifier(string identifier)
    {
        if (!_fileIds.TryGetPath(identifier, out var filePath))
        {
            throw new FileNotFoundException($"File '{identifier}' not found");
        }
        
        var parentPath = Path.GetDirectoryName(filePath)?.Replace('\\', '/') ?? "";
        if (!_fileIds.TryGetFileId(parentPath, out var parentFolderId))
        {
            throw new DirectoryNotFoundException("Parent directory not found");
        }
        return parentFolderId;
    }

    private string GetFolderParentIdentifier(string identifier)
    {
        if (!_fileIds.TryGetPath(identifier, out var folderPath))
        {
            throw new DirectoryNotFoundException($"Folder '{identifier}' not found");
        }
        
        var parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/') ?? "";
        if (!_fileIds.TryGetFileId(parentPath, out var parentFolderId))
        {
            throw new DirectoryNotFoundException("Parent directory not found");
        }
        return parentFolderId;
    }

    #endregion
}
