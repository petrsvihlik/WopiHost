using Microsoft.Extensions.Logging;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiAzureStorageProvider"/>.
/// </summary>
public partial class WopiAzureStorageProvider
{
    [LoggerMessage(Level = LogLevel.Information, Message = "WopiAzureStorageProvider initialized for container {container}")]
    private static partial void LogInitialized(ILogger logger, string container);
}
