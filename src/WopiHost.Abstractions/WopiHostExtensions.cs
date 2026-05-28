namespace WopiHost.Abstractions;

/// <summary>
/// Default pass-through implementation of <see cref="IWopiHostExtensions"/>. Customers extend
/// this class and override only the hooks they care about; un-overridden methods return the
/// input value unchanged or complete as a no-op.
/// </summary>
/// <remarks>
/// <para>
/// This is the default registration on <see cref="IWopiHostExtensions"/>, registered with
/// <c>TryAddSingleton</c> so hosts can override it by registering their subclass before or
/// after <c>AddWopi(...)</c>.
/// </para>
/// </remarks>
public class WopiHostExtensions : IWopiHostExtensions
{
    /// <inheritdoc />
    public virtual Task<WopiCheckFileInfo> OnCheckFileInfoAsync(WopiCheckFileInfoContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.CheckFileInfo);
    }

    /// <inheritdoc />
    public virtual Task<WopiCheckContainerInfo> OnCheckContainerInfoAsync(WopiCheckContainerInfoContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.CheckContainerInfo);
    }

    /// <inheritdoc />
    public virtual Task<WopiCheckFolderInfo> OnCheckFolderInfoAsync(WopiCheckFolderInfoContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.CheckFolderInfo);
    }

    /// <inheritdoc />
    public virtual Task<WopiCheckEcosystem> OnCheckEcosystemAsync(WopiCheckEcosystemContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.CheckEcosystem);
    }

    /// <inheritdoc />
    public virtual Task OnPutFileAsync(WopiPutFileContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnPutRelativeFileAsync(WopiPutRelativeFileContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
