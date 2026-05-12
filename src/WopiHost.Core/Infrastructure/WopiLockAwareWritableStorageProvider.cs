using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Decorator that consults <see cref="IWopiLockProvider"/> before mutating a resource and
/// throws <see cref="WopiResourceLockedException"/> when the target is locked. Wired in via
/// <c>services.AddWopiLockAwareWritableStorage()</c>.
/// </summary>
/// <remarks>
/// <para>
/// The WOPI controllers already validate locks before reaching the storage layer, so on the
/// hot path this decorator is a redundant guard. It earns its keep as a <em>defense-in-depth</em>
/// signal for two scenarios:
/// </para>
/// <list type="bullet">
/// <item>Non-WOPI code paths in the same host that resolve <see cref="IWopiWritableStorageProvider"/>
///   directly (admin tools, batch jobs, API endpoints) and would otherwise clobber a locked file.</item>
/// <item>Future controller refactors that accidentally drop the lock check — the storage layer
///   catches it instead of silently corrupting state.</item>
/// </list>
/// <para>
/// Only the mutating, single-resource methods (<see cref="DeleteWopiResource{T}"/>,
/// <see cref="RenameWopiResource{T}"/>) are guarded. <see cref="CreateWopiChildResource{T}"/>
/// targets a parent container and the new resource has no prior lock; <see cref="CheckValidName{T}"/>
/// and <see cref="GetSuggestedName{T}"/> are read-only.
/// </para>
/// </remarks>
public sealed class WopiLockAwareWritableStorageProvider : IWopiWritableStorageProvider
{
    private readonly IWopiWritableStorageProvider _inner;
    private readonly IWopiLockProvider _lockProvider;

    /// <summary>
    /// Creates a new lock-aware decorator over an existing writable storage provider.
    /// </summary>
    /// <param name="inner">the underlying writable storage provider being decorated.</param>
    /// <param name="lockProvider">the lock provider consulted before mutating writes.</param>
    public WopiLockAwareWritableStorageProvider(IWopiWritableStorageProvider inner, IWopiLockProvider lockProvider)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _lockProvider = lockProvider ?? throw new ArgumentNullException(nameof(lockProvider));
    }

    /// <inheritdoc />
    public int FileNameMaxLength => _inner.FileNameMaxLength;

    /// <inheritdoc />
    public Task<T?> CreateWopiChildResource<T>(string containerId, string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
        => _inner.CreateWopiChildResource<T>(containerId, name, cancellationToken);

    /// <inheritdoc />
    public Task<bool> CheckValidName<T>(string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
        => _inner.CheckValidName<T>(name, cancellationToken);

    /// <inheritdoc />
    public Task<string> GetSuggestedName<T>(string containerId, string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
        => _inner.GetSuggestedName<T>(containerId, name, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        // Inlined lock probe: pulled out of a private async helper because Infer# can't see
        // through cross-method async calls and flags the returned Task as potentially null.
        var existing = await _lockProvider.GetLockAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new WopiResourceLockedException(identifier, existing.LockId);
        }
        return await _inner.DeleteWopiResource<T>(identifier, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RenameWopiResource<T>(string identifier, string requestedName, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        var existing = await _lockProvider.GetLockAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new WopiResourceLockedException(identifier, existing.LockId);
        }
        return await _inner.RenameWopiResource<T>(identifier, requestedName, cancellationToken).ConfigureAwait(false);
    }
}
