namespace WopiHost.Abstractions;

/// <summary>
/// Details all WOPI container operation keywords
/// </summary>
public static class WopiContainerOperations
{
    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildcontainer
    /// </summary>
    public const string CreateChildContainer = "CREATE_CHILD_CONTAINER";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/deletecontainer
    /// </summary>
    public const string DeleteContainer = "DELETE_CONTAINER";
}
