namespace WopiHost.Abstractions;

/// <summary>
/// Response model for the <c>CheckEcosystem</c> operation.
/// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem"/>.
/// </summary>
/// <remarks>
/// All response properties are optional; per the WOPI spec an empty JSON response is valid.
/// Hosts can extend this DTO if they need to return additional fields.
/// </remarks>
public class WopiCheckEcosystem
{
    /// <summary>
    /// A Boolean value that should match the
    /// <see cref="WopiCheckFileInfo.SupportsContainers"/> property in <c>CheckFileInfo</c>.
    /// </summary>
    public bool SupportsContainers { get; set; }
}
