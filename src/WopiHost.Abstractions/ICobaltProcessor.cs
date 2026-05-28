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
    /// <param name="file">File to process. The MS-FSSHTTP protocol mutates content as part of
    /// the round-trip, so the writable interface is required (#420 item 1.2).</param>
    /// <param name="principal">User editing the file.</param>
    /// <param name="newContent">Partitions with new content (diffs).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An octet stream.</returns>
    Task<byte[]> ProcessCobalt(IWopiWritableFile file, ClaimsPrincipal principal, byte[] newContent, CancellationToken cancellationToken = default);
}
