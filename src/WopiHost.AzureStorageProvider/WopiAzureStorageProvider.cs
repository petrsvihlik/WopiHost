using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
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
    private readonly Lazy<Task> _initialization;

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

        // Set up root container (ID will be established during initialization)
        var rootId = _fileIds.GetPath(_options.RootPath ?? string.Empty) ?? 
                    _fileIds.AddFile(_options.RootPath ?? string.Empty);
        RootContainerPointer = new WopiAzureFolder(_options.ContainerName, rootId, _options.RootPath);

        // Initialize asynchronously to avoid deadlocks
        _initialization = new Lazy<Task>(InitializeAsync);
    }

    /// <summary>
    /// Performs async initialization of the provider.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            // Ensure container exists
            if (_options.CreateContainerIfNotExists)
            {
                await _containerClient.CreateIfNotExistsAsync(_options.ContainerPublicAccess);
            }

            // Scan existing blobs
            if (!_fileIds.WasScanned)
            {
                await _fileIds.ScanAllAsync(_containerClient, _options.RootPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Storage Provider");
            throw;
        }
    }

    /// <summary>
    /// Ensures the provider is initialized before performing operations.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        await _initialization.Value;
    }

    /// <inheritdoc/>
    public async Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync();

        if (!_fileIds.TryGetPath(identifier, out var blobPath))
        {
            return null;
        }

        return typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => await GetWopiFile(blobPath, identifier, cancellationToken) as T,
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => await GetWopiFolder(blobPath, identifier, cancellationToken) as T,
            _ => throw new NotSupportedException($"Unsupported resource type: {typeof(T).Name}"),
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string? identifier = null,
        string? searchPattern = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        var folderPath = _fileIds.GetPath(identifier ?? RootContainerPointer.Identifier) ?? 
                        throw new DirectoryNotFoundException($"Directory '{identifier}' not found");

        var prefix = string.IsNullOrEmpty(folderPath) ? null : folderPath.TrimEnd('/') + "/";

        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            if (blobItem.Properties.BlobType == Azure.Storage.Blobs.Models.BlobType.Block)
            {
                // Apply search pattern filtering if provided
                if (!string.IsNullOrEmpty(searchPattern))
                {
                    var fileName = Path.GetFileName(blobItem.Name);
                    if (!IsMatchingPattern(fileName, searchPattern))
                    {
                        continue;
                    }
                }

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
        await EnsureInitializedAsync();

        var folderPath = _fileIds.GetPath(identifier ?? RootContainerPointer.Identifier) ?? 
                        throw new DirectoryNotFoundException($"Directory '{identifier}' not found");

        var prefix = string.IsNullOrEmpty(folderPath) ? null : folderPath.TrimEnd('/') + "/";
        var folders = new HashSet<string>();

        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            if (IsValidBlobForContainer(blobItem))
            {
                var container = TryCreateContainerFromBlob(blobItem, folderPath, prefix, folders);
                if (container != null)
                {
                    yield return container;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiFolder>> GetAncestors<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();

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
    public async Task<bool> CheckValidName<T>(
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync();

        return typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => 
                name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.Length < FileNameMaxLength,
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => 
                name.IndexOfAny(Path.GetInvalidPathChars()) < 0,
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
        await EnsureInitializedAsync();

        if (!await CheckValidName<T>(name, cancellationToken))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(name));
        }
        
        if (!_fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }

        var newPath = string.IsNullOrEmpty(fullPath) ? name : $"{fullPath.TrimEnd('/')}/{name}";

        if (!await BlobExistsAsync(newPath))
        {
            return name;
        }

        var suggestedName = typeof(T) switch
        {
            { } when typeof(IWopiFolder).IsAssignableFrom(typeof(T)) => 
                await GenerateUniqueFolderName(name, fullPath),
            { } when typeof(IWopiFile).IsAssignableFrom(typeof(T)) => 
                await GenerateUniqueFileName(name, fullPath),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };

        // Validate length for files
        if (typeof(IWopiFile).IsAssignableFrom(typeof(T)))
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(suggestedName);
            if (nameWithoutExtension.Length > FileNameMaxLength)
            {
                throw new InvalidOperationException($"Generated name exceeds maximum length of {FileNameMaxLength} characters.");
            }
        }

        return suggestedName;
    }

    /// <inheritdoc/>
    public async Task<T?> CreateWopiChildResource<T>(
        string? containerId,
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();

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
            
            // Start the copy operation
            Azure.Storage.Blobs.Models.CopyFromUriOperation copyOperation = await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken);

            // Wait for copy to complete
            await copyOperation.WaitForCompletionAsync(cancellationToken);

            // Verify copy succeeded
            var destProperties = await destBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (destProperties.Value.CopyStatus is not Azure.Storage.Blobs.Models.CopyStatus.Success)
            {
                var statusDescription = destProperties.Value.CopyStatusDescription ?? "Unknown";
                throw new InvalidOperationException($"Failed to copy blob {fullPath}. Status: {destProperties.Value.CopyStatus} ({statusDescription})");
            }
            
            // Delete source after successful copy
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
                var relativePath = blobItem.Name[prefix.Length..];
                var newBlobPath = newPrefix + relativePath;
                
                var sourceBlob = _containerClient.GetBlobClient(blobItem.Name);
                var destBlob = _containerClient.GetBlobClient(newBlobPath);
                
                // Start the copy operation
                Azure.Storage.Blobs.Models.CopyFromUriOperation copyOperation = await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken);

                // Wait for copy to complete
                await copyOperation.WaitForCompletionAsync(cancellationToken);

                // Verify copy succeeded
                var destProperties = await destBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
                if (destProperties.Value.CopyStatus is not Azure.Storage.Blobs.Models.CopyStatus.Success)
                {
                    var statusDescription = destProperties.Value.CopyStatusDescription ?? "Unknown";
                    _logger.LogError("Failed to copy blob {BlobName}. Status: {CopyStatus} ({Description})", blobItem.Name, destProperties.Value.CopyStatus, statusDescription);
                    throw new InvalidOperationException($"Failed to copy blob {blobItem.Name}. Status: {destProperties.Value.CopyStatus} ({statusDescription})");
                }
                
                // Delete source after successful copy
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

    private async Task<IWopiFile?> GetWopiFile(string blobPath, string fileId, CancellationToken cancellationToken)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobPath);
            
            // Verify blob exists before creating WopiAzureFile
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                _logger.LogWarning("Blob {BlobPath} does not exist", blobPath);
                return null;
            }
            
            return new WopiAzureFile(blobClient, fileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file {BlobPath}", blobPath);
            return null;
        }
    }

    private Task<IWopiFolder?> GetWopiFolder(string blobPath, string folderId, CancellationToken cancellationToken)
    {
        try
        {
            // Folders in Azure Blob Storage are virtual, so we just create the wrapper
            return Task.FromResult<IWopiFolder?>(new WopiAzureFolder(_options.ContainerName, folderId, blobPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting folder {BlobPath}", blobPath);
            return Task.FromResult<IWopiFolder?>(null);
        }
    }

    private async Task<bool> BlobExistsAsync(string blobPath, bool isFolder = false)
    {
        try
        {
            if (isFolder)
            {
                // For folders, check if any blobs with the folder prefix exist
                var prefix = blobPath.TrimEnd('/') + "/";
                await foreach (var _ in _containerClient.GetBlobsAsync(prefix: prefix).ConfigureAwait(false))
                {
                    return true; // If at least one blob exists with this prefix, folder exists
                }
                
                // Also check for the .folder marker
                var folderMarkerPath = prefix + ".folder";
                var markerClient = _containerClient.GetBlobClient(folderMarkerPath);
                var markerExists = await markerClient.ExistsAsync();
                return markerExists.Value;
            }
            else
            {
                // For files, check if the specific blob exists
                var blobClient = _containerClient.GetBlobClient(blobPath);
                var response = await blobClient.ExistsAsync();
                return response.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if blob exists: {BlobPath}", blobPath);
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
        return GetParentIdentifier(identifier, "File");
    }

    private string GetFolderParentIdentifier(string identifier)
    {
        return GetParentIdentifier(identifier, "Folder");
    }

    private string GetParentIdentifier(string identifier, string resourceType)
    {
        if (!_fileIds.TryGetPath(identifier, out var resourcePath))
        {
            if (resourceType == "File")
            {
                throw new FileNotFoundException($"File '{identifier}' not found");
            }
            else
            {
                throw new DirectoryNotFoundException($"Folder '{identifier}' not found");
            }
        }
        
        var parentPath = Path.GetDirectoryName(resourcePath)?.Replace('\\', '/') ?? "";
        if (!_fileIds.TryGetFileId(parentPath, out var parentFolderId))
        {
            throw new DirectoryNotFoundException("Parent directory not found");
        }
        return parentFolderId;
    }

    private static bool IsValidBlobForContainer(BlobItem blobItem)
    {
        return blobItem.Properties.BlobType == Azure.Storage.Blobs.Models.BlobType.Block;
    }

    private WopiAzureFolder? TryCreateContainerFromBlob(BlobItem blobItem, string folderPath, string? prefix, HashSet<string> folders)
    {
        var relativePath = GetRelativePath(blobItem.Name, prefix);
        var pathParts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (pathParts.Length <= 1)
        {
            return null;
        }

        var folderName = pathParts[0];
        var fullFolderPath = string.IsNullOrEmpty(folderPath) ? folderName : $"{folderPath.TrimEnd('/')}/{folderName}";
        
        if (!folders.Add(fullFolderPath))
        {
            return null;
        }

        var folderId = _fileIds.TryGetFileId(fullFolderPath, out var id) ? id : _fileIds.AddFile(fullFolderPath);
        return new WopiAzureFolder(_options.ContainerName, folderId, fullFolderPath);
    }

    private static string GetRelativePath(string blobName, string? prefix)
    {
        return string.IsNullOrEmpty(prefix) ? blobName : blobName[prefix.Length..];
    }

    private static bool IsMatchingPattern(string fileName, string pattern)
    {
        // Convert DOS wildcard pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private async Task<string> GenerateUniqueFolderName(string name, string fullPath)
    {
        var newName = name;
        var counter = 1;
        var basePath = fullPath.TrimEnd('/');
        
        while (await BlobExistsAsync($"{basePath}/{newName}"))
        {
            newName = $"{name} ({counter++})";
        }
        
        return newName;
    }

    private async Task<string> GenerateUniqueFileName(string name, string fullPath)
    {
        var extension = Path.GetExtension(name);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        var newName = name;
        var counter = 1;
        var basePath = fullPath.TrimEnd('/');
        
        while (await BlobExistsAsync($"{basePath}/{newName}"))
        {
            newName = $"{nameWithoutExt} ({counter++}){extension}";
        }
        
        return newName;
    }

    #endregion
}
