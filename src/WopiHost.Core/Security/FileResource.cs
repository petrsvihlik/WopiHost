namespace WopiHost.Core.Security;

/// <summary>
/// Represents a resource for a resource-based authroization.
/// </summary>
/// <remarks>
/// Creates an object representing a resource for a resource-based authroization.
/// </remarks>
/// <param name="fileId">Identifier of a resource.</param>
public class FileResource(string fileId)
{
    /// <summary>
    /// Identifier of a resource.
    /// </summary>
    public string FileId { get; } = fileId;
}
