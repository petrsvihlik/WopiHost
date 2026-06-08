using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Context passed to <see cref="IWopiHostExtensions.OnAddActivitiesAsync"/> when a WOPI client
/// reports activities (comments / mentions) on a file. The host reacts here — sending
/// notifications, surfacing an activity feed, prompting to share with a mentioned user — while the
/// core decides each activity's protocol response status.
/// </summary>
/// <param name="User">The authenticated principal, if any.</param>
/// <param name="File">The file the activities target.</param>
/// <param name="Activities">The reported activities, in request order.</param>
public sealed record WopiAddActivitiesContext(
    ClaimsPrincipal? User,
    IWopiFile File,
    IReadOnlyList<WopiActivity> Activities);
