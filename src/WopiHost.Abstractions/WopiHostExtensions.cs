namespace WopiHost.Abstractions;

/// <summary>
/// Provides the default pass-through implementation of <see cref="IWopiHostExtensions"/>. Hosts derive
/// from this class and override only the hooks they need; the other methods return the
/// input value unchanged or complete as a no-op.
/// </summary>
/// <remarks>
/// Registered with <c>TryAddSingleton</c>, so a host can replace it by registering its subclass
/// before or after <c>AddWopi(...)</c>.
/// </remarks>
public class WopiHostExtensions : IWopiHostExtensions
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public virtual Task<WopiCheckFileInfo> OnCheckFileInfoAsync(WopiCheckFileInfoContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.CheckFileInfo);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public virtual Task<WopiCheckContainerInfo> OnCheckContainerInfoAsync(WopiCheckContainerInfoContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.CheckContainerInfo);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public virtual Task<WopiCheckFolderInfo> OnCheckFolderInfoAsync(WopiCheckFolderInfoContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.CheckFolderInfo);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public virtual Task<WopiCheckEcosystem> OnCheckEcosystemAsync(WopiCheckEcosystemContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.CheckEcosystem);
    }

    /// <inheritdoc />
    public virtual Task OnPutFileAsync(WopiPutFileContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnPutRelativeFileAsync(WopiPutRelativeFileContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnAddActivitiesAsync(WopiAddActivitiesContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
