using System.Xml.Linq;

namespace WopiHost.Discovery;

/// <summary>
/// Provides access to the WOPI discovery XML file.
/// https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/online/discovery
/// </summary>
public interface IDiscoveryFileProvider
{
    /// <summary>
    /// Gets an object representation of an XML file representing the capabilities of a WOPI client.
    /// </summary>
    /// <returns>An object representation of WOPI discovery XML file.</returns>
    Task<XElement> GetDiscoveryXmlAsync();
}