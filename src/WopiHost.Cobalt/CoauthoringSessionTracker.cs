using System.Collections.Concurrent;
using Cobalt;

namespace WopiHost.Cobalt;

/// <summary>
/// Tracks active co-authoring sessions per file to provide accurate editor counts
/// and co-authoring status to OOS/OWA via the Cobalt protocol.
/// </summary>
/// <remarks>
/// Fixes https://github.com/petrsvihlik/WopiHost/issues/139 — without shared session tracking,
/// each Cobalt request creates a fresh <see cref="CobaltHostLockingStore"/> that always reports
/// <see cref="CoauthStatusType.Alone"/> and an empty editors table, causing older OOS versions
/// to show duplicate user names when the same person opens a document in multiple tabs.
/// </remarks>
public class CoauthoringSessionTracker
{
    /// <summary>
    /// Represents a single editing session (one browser tab / WOPI session).
    /// </summary>
    /// <param name="UserId">The authenticated user's identifier.</param>
    /// <param name="UserName">The user's display name.</param>
    /// <param name="LastActivity">When this session was last refreshed.</param>
    public record EditorSession(string UserId, string UserName, DateTimeOffset LastActivity);

    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);

    // fileId → (userId → session)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, EditorSession>> _sessions = new();

    /// <summary>
    /// Registers or refreshes a user's editing session for the given file.
    /// Multiple tabs from the same user are deduplicated by user ID.
    /// </summary>
    public void AddOrRefreshSession(string fileId, string userId, string userName)
    {
        var fileSessions = _sessions.GetOrAdd(fileId, _ => new ConcurrentDictionary<string, EditorSession>());
        fileSessions[userId] = new EditorSession(userId, userName, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Removes a user's editing session for the given file.
    /// </summary>
    public void RemoveSession(string fileId, string userId)
    {
        if (_sessions.TryGetValue(fileId, out var fileSessions))
        {
            fileSessions.TryRemove(userId, out _);
            // Clean up empty file entries
            if (fileSessions.IsEmpty)
            {
                _sessions.TryRemove(fileId, out _);
            }
        }
    }

    /// <summary>
    /// Returns the number of distinct active editors for a file.
    /// </summary>
    public int GetActiveEditorCount(string fileId)
    {
        if (!_sessions.TryGetValue(fileId, out var fileSessions))
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow - SessionTimeout;
        return fileSessions.Values.Count(s => s.LastActivity > cutoff);
    }

    /// <summary>
    /// Returns whether the given user is the only active editor for the file.
    /// </summary>
    public bool IsAlone(string fileId, string userId)
    {
        if (!_sessions.TryGetValue(fileId, out var fileSessions))
        {
            return true;
        }

        var cutoff = DateTimeOffset.UtcNow - SessionTimeout;
        var activeEditors = fileSessions.Values
            .Where(s => s.LastActivity > cutoff)
            .ToList();

        return activeEditors.Count == 0 || activeEditors.All(s => s.UserId == userId);
    }

    /// <summary>
    /// Returns the editors table for the Cobalt protocol.
    /// </summary>
    public Dictionary<string, EditorsTableEntry> GetEditorsTable(string fileId)
    {
        var result = new Dictionary<string, EditorsTableEntry>();
        if (!_sessions.TryGetValue(fileId, out var fileSessions))
        {
            return result;
        }

        var cutoff = DateTimeOffset.UtcNow - SessionTimeout;
        foreach (var session in fileSessions.Values.Where(s => s.LastActivity > cutoff))
        {
            result[session.UserId] = new EditorsTableEntry
            {
                HasEditPermission = true,
                UserName = session.UserName,
                UserLogin = session.UserId
            };
        }

        return result;
    }

    /// <summary>
    /// Returns the appropriate co-authoring status for a file.
    /// </summary>
    public CoauthStatusType GetCoauthStatus(string fileId, string userId)
    {
        var editorCount = GetActiveEditorCount(fileId);
        return editorCount <= 1 && IsAlone(fileId, userId)
            ? CoauthStatusType.Alone
            : CoauthStatusType.Coauthoring;
    }
}
