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
/// Only the mutating, single-resource methods (<see cref="DeleteWopiFile"/>,
/// <see cref="DeleteWopiContainer"/>, <see cref="RenameWopiFile"/>, <see cref="RenameWopiContainer"/>)
/// are guarded. The create methods target a parent container and the new resource has no prior
/// lock; the validation / suggested-name methods are read-only.
/// </para>
/// </remarks>
/// <param name="inner">the underlying writable storage provider being decorated.</param>
/// <param name="lockProvider">the lock provider consulted before mutating writes.</param>
public sealed class WopiLockAwareWritableStorageProvider(
    IWopiWritableStorageProvider inner,
    IWopiLockProvider lockProvider) : IWopiWritableStorageProvider
{
    private readonly IWopiWritableStorageProvider _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IWopiLockProvider _lockProvider = lockProvider ?? throw new ArgumentNullException(nameof(lockProvider));

    /// <inheritdoc />
    public int FileNameMaxLength => _inner.FileNameMaxLength;

    /// <inheritdoc />
    public Task<IWopiWritableFile?> CreateWopiChildFile(string containerId, string name, CancellationToken cancellationToken = default)
        => _inner.CreateWopiChildFile(containerId, name, cancellationToken);

    /// <inheritdoc />
    public Task<IWopiWritableFile?> GetWritableFile(string identifier, CancellationToken cancellationToken = default)
        => _inner.GetWritableFile(identifier, cancellationToken);

    /// <inheritdoc />
    public Task<IWopiContainer?> CreateWopiChildContainer(string containerId, string name, CancellationToken cancellationToken = default)
        => _inner.CreateWopiChildContainer(containerId, name, cancellationToken);

    /// <inheritdoc />
    public Task<bool> CheckValidFileName(string name, CancellationToken cancellationToken = default)
        => _inner.CheckValidFileName(name, cancellationToken);

    /// <inheritdoc />
    public Task<bool> CheckValidContainerName(string name, CancellationToken cancellationToken = default)
        => _inner.CheckValidContainerName(name, cancellationToken);

    /// <inheritdoc />
    public Task<string> GetSuggestedFileName(string containerId, string name, CancellationToken cancellationToken = default)
        => _inner.GetSuggestedFileName(containerId, name, cancellationToken);

    /// <inheritdoc />
    public Task<string> GetSuggestedContainerName(string containerId, string name, CancellationToken cancellationToken = default)
        => _inner.GetSuggestedContainerName(containerId, name, cancellationToken);

    // The lock probe (GetLockAsync + throw-if-locked) is inlined into each of the four
    // mutating methods below rather than extracted into a private async helper. Static analysis
    // can't see through cross-method async calls and flags a shared helper's returned Task as
    // potentially null at every caller, so inlining avoids the false positive.

    /// <inheritdoc />
    public async Task<bool> DeleteWopiFile(string identifier, CancellationToken cancellationToken = default)
    {
        var existing = await _lockProvider.GetLockAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new WopiResourceLockedException(identifier, existing.LockId);
        }
        return await _inner.DeleteWopiFile(identifier, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken = default)
    {
        var existing = await _lockProvider.GetLockAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new WopiResourceLockedException(identifier, existing.LockId);
        }
        return await _inner.DeleteWopiContainer(identifier, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RenameWopiFile(string identifier, string requestedName, CancellationToken cancellationToken = default)
    {
        var existing = await _lockProvider.GetLockAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new WopiResourceLockedException(identifier, existing.LockId);
        }
        return await _inner.RenameWopiFile(identifier, requestedName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RenameWopiContainer(string identifier, string requestedName, CancellationToken cancellationToken = default)
    {
        var existing = await _lockProvider.GetLockAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new WopiResourceLockedException(identifier, existing.LockId);
        }
        return await _inner.RenameWopiContainer(identifier, requestedName, cancellationToken).ConfigureAwait(false);
    }
}
