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
    /// <param name="discoverer">Discoverer instance.</param>
    /// <param name="extension">File extension to consider (without the leading dot).</param>
    /// <param name="action">WOPI action to consider.</param>
    public static async Task<bool> RequiresCobaltAsync(this IDiscoverer discoverer, string extension, WopiActionEnum action)
    {
        ArgumentNullException.ThrowIfNull(discoverer);
        var requirements = await discoverer.GetActionRequirementsAsync(extension, action).ConfigureAwait(false);
        return requirements is not null && requirements.Contains(CobaltRequirement);
    }
}
