using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Cobalt;
using Cobalt.Base;
using Microsoft.Extensions.Logging;

namespace WopiHost.Cobalt;

/// <summary>
/// Tracks active co-authoring sessions per file to provide accurate editor counts
/// and co-authoring status to OOS/OWA via the Cobalt protocol.
/// </summary>
/// <param name="logger">The logger.</param>
/// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
/// <remarks>
/// <para>
/// Session state is static, so it is shared by every instance in the process. Sessions with no
/// activity for 30 minutes are ignored.
/// </para>
/// <para>
/// A <see cref="CobaltHostLockingStore"/> that always reported <see cref="CoauthStatusType.Alone"/>
/// and an empty editors table would make older OOS versions show duplicate user names when the
/// same person opens a document in multiple tabs.
/// </para>
/// </remarks>
public partial class CoauthoringSessionTracker(ILogger<CoauthoringSessionTracker> logger)
{
    private readonly ILogger<CoauthoringSessionTracker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Represents a single editing session (one browser tab / WOPI session).
    /// </summary>
    /// <param name="UserId">The authenticated user's identifier.</param>
    /// <param name="UserName">The user's display name.</param>
    /// <param name="LastActivity">The time this session was last refreshed.</param>
    public record EditorSession(string UserId, string UserName, DateTimeOffset LastActivity);

    private static readonly TimeSpan s_sessionTimeout = TimeSpan.FromMinutes(30);

    // fileId → (userId → session) — static so state is shared across all CobaltHostLockingStore instances
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, EditorSession>> s_sessions = new();

    /// <summary>
    /// Registers or refreshes a user's editing session for the given file.
    /// Multiple tabs from the same user are deduplicated by user ID.
    /// </summary>
    /// <param name="fileId">The identifier of the file being edited.</param>
    /// <param name="userId">The identifier of the editing user.</param>
    /// <param name="userName">The display name of the editing user.</param>
    public void AddOrRefreshSession(string fileId, string userId, string userName)
    {
        var fileSessions = s_sessions.GetOrAdd(fileId, _ => new ConcurrentDictionary<string, EditorSession>());
        var added = !fileSessions.ContainsKey(userId);
        fileSessions[userId] = new EditorSession(userId, userName, DateTimeOffset.UtcNow);
        if (added)
        {
            LogEditorJoined(_logger, userId, userName, fileId);
        }
    }

    /// <summary>
    /// Removes a user's editing session for the given file.
    /// </summary>
    /// <param name="fileId">The identifier of the file.</param>
    /// <param name="userId">The identifier of the user whose session to remove.</param>
    public void RemoveSession(string fileId, string userId)
    {
        if (s_sessions.TryGetValue(fileId, out var fileSessions))
        {
            if (fileSessions.TryRemove(userId, out _))
            {
                LogEditorLeft(_logger, userId, fileId);
            }
            if (fileSessions.IsEmpty)
            {
                s_sessions.TryRemove(fileId, out _);
            }
        }
    }

    /// <summary>
    /// Gets the number of distinct active editors for a file.
    /// </summary>
    /// <param name="fileId">The identifier of the file.</param>
    /// <returns>The number of distinct users with an active session on the file.</returns>
    public int GetActiveEditorCount(string fileId)
    {
        if (!s_sessions.TryGetValue(fileId, out var fileSessions))
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow - s_sessionTimeout;
        return fileSessions.Values.Count(s => s.LastActivity > cutoff);
    }

    /// <summary>
    /// Determines whether the given user is the only active editor for the file.
    /// </summary>
    /// <param name="fileId">The identifier of the file.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns><see langword="true"/> if no other user has an active session on the file; otherwise, <see langword="false"/>.</returns>
    public bool IsAlone(string fileId, string userId)
    {
        if (!s_sessions.TryGetValue(fileId, out var fileSessions))
        {
            return true;
        }

        var cutoff = DateTimeOffset.UtcNow - s_sessionTimeout;
        var activeEditors = fileSessions.Values
            .Where(s => s.LastActivity > cutoff)
            .ToList();

        return activeEditors.Count == 0 || activeEditors.All(s => s.UserId == userId);
    }

    /// <summary>
    /// Gets the Cobalt editors table for a file.
    /// </summary>
    /// <param name="fileId">The identifier of the file.</param>
    /// <returns>A table with one entry per active editor; empty if the file has none.</returns>
    /// <remarks>
    /// The editors table is keyed by <see cref="ArrayGuid"/> (per-editor ClientId)
    /// and entries are <see cref="EditorsTableEntryNew"/>. WopiHost has no real
    /// Cobalt ClientId, so a stable one is derived per-user by hashing the user id,
    /// which collapses a user's multiple tabs to a single editor row.
    /// </remarks>
    public EditorsTable GetEditorsTable(string fileId)
    {
        var result = new EditorsTable();
        if (!s_sessions.TryGetValue(fileId, out var fileSessions))
        {
            return result;
        }

        var cutoff = DateTimeOffset.UtcNow - s_sessionTimeout;
        foreach (var session in fileSessions.Values.Where(s => s.LastActivity > cutoff))
        {
            var clientId = DeriveClientId(session.UserId);
            result[clientId] = new EditorsTableEntryNew(
                dtTimeout: session.LastActivity.UtcDateTime + s_sessionTimeout,
                clientId: clientId,
                userName: session.UserName,
                userLogin: session.UserId,
                sipAddress: null,
                emailAddress: null,
                hasEditPermission: true);
        }

        return result;
    }

    private static ArrayGuid DeriveClientId(string userId)
    {
        // 16-byte MD5 of the user id maps cleanly onto ArrayGuid's underlying
        // byte array. Not used for security — just a stable per-user identity
        // so the same user across tabs collapses to a single editor row.
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(userId ?? string.Empty));
        return new ArrayGuid(bytes);
    }

    /// <summary>
    /// Gets the co-authoring status of a file from the perspective of the given user.
    /// </summary>
    /// <param name="fileId">The identifier of the file.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <returns><see cref="CoauthStatusType.Alone"/> if the user is the only active editor; otherwise, <see cref="CoauthStatusType.Coauthoring"/>.</returns>
    public CoauthStatusType GetCoauthStatus(string fileId, string userId)
    {
        var editorCount = GetActiveEditorCount(fileId);
        return editorCount <= 1 && IsAlone(fileId, userId)
            ? CoauthStatusType.Alone
            : CoauthStatusType.Coauthoring;
    }
}
