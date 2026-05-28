using System.Collections.ObjectModel;
using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Context for the <c>PutFile</c> operation, raised after the host has successfully written
/// the new file contents and is about to return the response.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Editors"/> collection is parsed from the optional <c>X-WOPI-Editors</c>
/// request header per the WOPI <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile">PutFile</see>
/// spec: a comma-delimited list of <see cref="WopiCheckFileInfo.UserId"/> values for every
/// user who contributed to the changes flushed in this PutFile request. Useful for audit
/// trails and last-touch metadata. The list is empty when the header is absent or empty.
/// </para>
/// <para>
/// The callback runs after the write has completed successfully but before the controller
/// emits the 200 response. Throwing here turns the response into a 500 — for non-fatal
/// bookkeeping (audit log, last-edit timestamp), swallow exceptions inside the handler.
/// </para>
/// </remarks>
/// <param name="User">the principal that issued the PutFile request.</param>
/// <param name="File">the file resource that was just written.</param>
/// <param name="Editors">user ids who contributed to this PutFile, parsed from
/// <c>X-WOPI-Editors</c>. Empty when the header is absent.</param>
public record WopiPutFileContext(
    ClaimsPrincipal? User,
    IWopiFile File,
    ReadOnlyCollection<string> Editors);
