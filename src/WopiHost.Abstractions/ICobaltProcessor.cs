using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Processes MS-FSSHTTP (Cobalt) requests.
/// </summary>
public interface ICobaltProcessor
{
    /// <summary>
    /// Executes an MS-FSSHTTP request batch against <paramref name="file"/>.
    /// </summary>
    /// <param name="file">The file to process. MS-FSSHTTP mutates content as part of the
    /// round-trip, so the writable interface is required.</param>
    /// <param name="principal">The user editing the file.</param>
    /// <param name="newContent">The serialized MS-FSSHTTP request body, including any content
    /// partitions (diffs).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The serialized MS-FSSHTTP response body.</returns>
    Task<byte[]> ProcessCobalt(IWopiWritableFile file, ClaimsPrincipal principal, byte[] newContent, CancellationToken cancellationToken = default);
}
