namespace WopiHost.Abstractions;

/// <summary>
/// Provides the <c>X-WOPI-Override</c> values of the WOPI ecosystem operations.
/// </summary>
public static class WopiEcosystemOperations
{
    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getfilewopisrc">GetFileWopiSrc (ecosystem)</see>
    /// operation.
    /// </summary>
    public const string GetWopiSrcWithAccessToken = "GET_WOPI_SRC_WITH_ACCESS_TOKEN";
}
