namespace WopiHost.Abstractions;

/// <summary>
/// Represents the response of the
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem">CheckEcosystem</see>
/// operation.
/// </summary>
/// <remarks>
/// All response properties are optional; per the WOPI spec an empty JSON response is valid.
/// Hosts can extend this DTO if they need to return additional fields.
/// </remarks>
public class WopiCheckEcosystem
{
    /// <summary>
    /// Gets or sets a value indicating whether the host supports containers.
    /// Should match <see cref="WopiCheckFileInfo.SupportsContainers"/>.
    /// </summary>
    public bool SupportsContainers { get; set; }
}
