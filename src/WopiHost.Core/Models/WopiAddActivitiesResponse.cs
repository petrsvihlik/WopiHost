using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Represents the response body of the <c>AddActivities</c> operation: one result per submitted activity, in order.
/// </summary>
/// <param name="ActivityResponses">The per-activity results.</param>
/// <seealso href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/fdc52ab9-b359-4465-a8c7-6aa98aa12e06">MS-WOPI AddActivities</seealso>
public sealed record WopiAddActivitiesResponse(IReadOnlyList<WopiActivityResponse> ActivityResponses);

/// <summary>Represents one entry of <see cref="WopiAddActivitiesResponse"/>.</summary>
/// <param name="Id">The activity id, echoed verbatim from the request.</param>
/// <param name="Status">The per-activity result status.</param>
public sealed record WopiActivityResponse(string Id, WopiActivityStatus Status);
