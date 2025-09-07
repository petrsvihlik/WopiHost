# WopiHost.AzureStorageProvider Sample

This sample demonstrates how to use the WopiHost.AzureStorageProvider with Azure Blob Storage.

## Prerequisites

1. An Azure Storage Account
2. .NET 8.0 or later
3. Visual Studio 2022 or VS Code

## Configuration

1. Update the `appsettings.json` file with your Azure Storage connection details:

```json
{
  "WopiHost": {
    "StorageOptions": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=yourkey;EndpointSuffix=core.windows.net",
      "ContainerName": "wopi-files",
      "RootPath": "documents",
      "UseManagedIdentity": false,
      "CreateContainerIfNotExists": true
    },
    "ClientUrl": "https://your-office-online-server.com/hosting/discovery"
  }
}
```

## Running the Sample

1. Navigate to the sample directory:
   ```bash
   cd sample/WopiHost.AzureStorageProvider
   ```

2. Restore packages:
   ```bash
   dotnet restore
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

4. Open your browser and navigate to `https://localhost:5001` (or the URL shown in the console)
5. The OpenAPI specification will be available at `/openapi/v1.json`

## Features Demonstrated

- Azure Blob Storage integration
- File upload/download operations
- Container management
- Security handling
- WOPI protocol implementation

## Authentication Options

The sample supports multiple authentication methods:

### Connection String
```json
{
  "StorageOptions": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "wopi-files"
  }
}
```

### Account Key
```json
{
  "StorageOptions": {
    "AccountName": "yourstorageaccount",
    "AccountKey": "yourkey",
    "ContainerName": "wopi-files"
  }
}
```

### Managed Identity
```json
{
  "StorageOptions": {
    "AccountName": "yourstorageaccount",
    "UseManagedIdentity": true,
    "ContainerName": "wopi-files"
  }
}
```

## Testing

You can test the WOPI endpoints using tools like Postman or curl:

- Discovery: `GET /wopi/discovery`
- CheckFileInfo: `GET /wopi/files/{fileId}`
- GetFile: `GET /wopi/files/{fileId}/contents`

## Troubleshooting

1. **Container not found**: Ensure the container exists or set `CreateContainerIfNotExists` to `true`
2. **Authentication failed**: Verify your connection string or account credentials
3. **Permission denied**: Check that your Azure Storage account has the necessary permissions
