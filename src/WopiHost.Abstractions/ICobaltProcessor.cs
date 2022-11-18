using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Service that can process MS-FSSHTTP requests.
/// </summary>
public interface ICobaltProcessor
{
    /// <summary>
    /// Gets content of a MS-FSSHTTP update action.
    /// </summary>
    /// <param name="file">File to process</param>
    /// <param name="principal">User editing the file</param>
    /// <param name="newContent">Partitions with new content (diffs)</param>
    /// <returns>An octet stream.</returns>
    Action<Stream> ProcessCobalt(IWopiFile file, ClaimsPrincipal principal, byte[] newContent);
}
