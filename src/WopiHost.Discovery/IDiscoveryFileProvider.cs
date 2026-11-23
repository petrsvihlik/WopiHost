using System.Xml.Linq;

namespace WopiHost.Discovery;

/// <summary>
/// Provides access to the WOPI discovery XML file.
/// </summary>
/// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery">WOPI discovery</seealso>
public interface IDiscoveryFileProvider
{
    /// <summary>
    /// Gets the discovery XML that describes the capabilities of a WOPI client.
    /// </summary>
    /// <returns>The root element of the discovery XML.</returns>
    Task<XElement> GetDiscoveryXmlAsync();
}