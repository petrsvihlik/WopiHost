namespace WopiHost.Discovery;

/// <summary>
/// Determines network zones.
/// </summary>
public enum NetZoneEnum
{
    /// <summary>
    /// Represents a network zone where the WOPI client is deployed on an internal network using a fully qualified domain name (FQDN) via the HTTP.
    /// </summary>
    InternalHttp,

    /// <summary>
    /// Represents a network zone where the WOPI client is deployed on an internal network using a fully qualified domain name (FQDN) via the HTTPS.
    /// </summary>
    InternalHttps,

    /// <summary>
    /// Represents a network zone where the WOPI client is deployed with a fully qualified domain name (FQDN) accessible from Internet via the HTTP.
    /// </summary>
    ExternalHttp,

    /// <summary>
    /// Represents a network zone where the WOPI client is deployed with a fully qualified domain name (FQDN) accessible from Internet via the HTTPS.
    /// </summary>
    ExternalHttps
}