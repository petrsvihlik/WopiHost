namespace WopiHost.Discovery;

/// <summary>
/// Defines options that expose the WOPI client URL the discovery file is fetched from.
/// </summary>
public interface IDiscoveryOptions
{
    /// <summary>
    /// Gets the base URL of the WOPI client.
    /// </summary>
    Uri? ClientUrl { get; }
} 