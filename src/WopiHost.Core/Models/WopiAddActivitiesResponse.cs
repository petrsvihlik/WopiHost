using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Response body for the AddActivities operation — one result per submitted activity, in order.
/// <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/fdc52ab9-b359-4465-a8c7-6aa98aa12e06"/>.
/// </summary>
/// <param name="ActivityResponses">Per-activity results.</param>
public sealed record WopiAddActivitiesResponse(IReadOnlyList<WopiActivityResponse> ActivityResponses);

/// <summary>One entry of <see cref="WopiAddActivitiesResponse"/>.</summary>
/// <param name="Id">The activity id, echoed verbatim from the request.</param>
/// <param name="Status">The per-activity result status.</param>
public sealed record WopiActivityResponse(string Id, WopiActivityStatus Status);
