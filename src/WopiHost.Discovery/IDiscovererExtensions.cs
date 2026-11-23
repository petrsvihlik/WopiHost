using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery;

/// <summary>
/// Convenience extension methods over <see cref="IDiscoverer"/> that derive from primitives
/// already exposed by the interface.
/// </summary>
public static class IDiscovererExtensions
{
    private const string CobaltRequirement = "cobalt";

    /// <summary>
    /// Determines whether files with the given extension require MS-FSSHTTP (Cobalt) for the
    /// specified action — i.e. whether the discovery file lists <c>cobalt</c> in the action's
    /// <c>requires</c> attribute.
    /// </summary>
    /// <param name="discoverer">The discoverer to query.</param>
    /// <param name="extension">The file extension to consider (without the leading dot).</param>
    /// <param name="action">The WOPI action to consider.</param>
    /// <returns><see langword="true"/> if the action requires Cobalt for the extension; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="discoverer"/> is <see langword="null"/>.</exception>
    public static async Task<bool> RequiresCobaltAsync(this IDiscoverer discoverer, string extension, WopiActionEnum action)
    {
        ArgumentNullException.ThrowIfNull(discoverer);
        var requirements = await discoverer.GetActionRequirementsAsync(extension, action).ConfigureAwait(false);
        return requirements is not null && requirements.Contains(CobaltRequirement);
    }
}
