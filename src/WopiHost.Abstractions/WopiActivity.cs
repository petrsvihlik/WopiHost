using System.Text.Json;

namespace WopiHost.Abstractions;

/// <summary>
/// A single activity reported by the WOPI client on the
/// <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/fdc52ab9-b359-4465-a8c7-6aa98aa12e06">AddActivities</see>
/// operation — most commonly a comment add / update / delete, optionally referencing people.
/// </summary>
/// <param name="Type">The activity type, e.g. <c>comment</c>. Unrecognized types are answered with
/// <see cref="WopiActivityStatus.NotSupported"/>.</param>
/// <param name="Id">The client-assigned activity id. Echoed back verbatim in the response.</param>
/// <param name="Data">The type-specific payload (comment text, content/navigation ids, people, …),
/// surfaced as raw JSON so a host can read whatever fields it needs without the contract pinning them.</param>
public sealed record WopiActivity(string? Type, string? Id, JsonElement? Data);
