namespace WopiHost.Discovery;

/// <summary>
/// Specifies the network zone of a WOPI client, matching the <c>net-zone</c> names in the discovery XML.
/// </summary>
public enum NetZoneEnum
{
    /// <summary>
    /// Represents a network zone where the WOPI client is deployed on an internal network using a fully qualified domain name (FQDN) over HTTP.
    /// </summary>
    InternalHttp,

    /// <summary>
    /// Represents a network zone where the WOPI client is deployed on an internal network using a fully qualified domain name (FQDN) over HTTPS.
    /// </summary>
    InternalHttps,

    /// <summary>
    /// Represents a network zone where the WOPI client is deployed with a fully qualified domain name (FQDN) accessible from the Internet over HTTP.
    /// </summary>
    ExternalHttp,

    /// <summary>
    /// Represents a network zone where the WOPI client is deployed with a fully qualified domain name (FQDN) accessible from the Internet over HTTPS.
    /// </summary>
    ExternalHttps
}