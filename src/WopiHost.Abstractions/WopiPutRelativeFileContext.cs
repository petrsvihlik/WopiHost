using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Context for the <c>PutRelativeFile</c> operation, raised after the host has successfully
/// created and written the new child file.
/// </summary>
/// <remarks>
/// <para>
/// Surfaces the two optional WOPI request headers that the controller does not otherwise act on:
/// <c>X-WOPI-FileConversion</c> (signalled via <see cref="IsFileConversion"/>) and
/// <c>X-WOPI-Size</c> (parsed into <see cref="DeclaredSize"/>). See the WOPI
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see>
/// spec for the meaning of each.
/// </para>
/// <para>
/// Hosts that want to handle binary document conversions specially (suppress notifications,
/// bypass quota, mark the resulting file as derived) read <see cref="IsFileConversion"/> from
/// this callback. Hosts that need an early size-budget check pre-write should instead enforce
/// host-options-level limits on the actual stream length.
/// </para>
/// <para>
/// The callback runs after the write has completed but before the controller emits the response.
/// Throwing turns the response into a 500.
/// </para>
/// </remarks>
/// <param name="User">the principal that issued the PutRelativeFile request.</param>
/// <param name="OriginalFile">the file the operation was invoked on (the "current file" in the spec).</param>
/// <param name="NewFile">the newly created child file containing the uploaded contents.</param>
/// <param name="IsFileConversion"><see langword="true"/> when the <c>X-WOPI-FileConversion</c>
/// header was present, indicating this PutRelativeFile is part of a binary document conversion.</param>
/// <param name="DeclaredSize">value parsed from <c>X-WOPI-Size</c>, or <see langword="null"/>
/// when the header was absent or unparseable.</param>
public record WopiPutRelativeFileContext(
    ClaimsPrincipal? User,
    IWopiFile OriginalFile,
    IWopiFile NewFile,
    bool IsFileConversion,
    long? DeclaredSize);
