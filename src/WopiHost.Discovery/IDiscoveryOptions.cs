namespace WopiHost.Discovery;

/// <summary>
/// Interface for options that contain discovery configuration including the client URL.
/// </summary>
public interface IDiscoveryOptions
{
    /// <summary>
    /// The base URL of the WOPI client.
    /// </summary>
    Uri? ClientUrl { get; }
} 