using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Url;

/// <summary>
/// Generates WOPI client URLs from the URL templates in the discovery file.
/// </summary>
/// <param name="discoverer">The provider of WOPI discovery data.</param>
/// <param name="logger">The logger.</param>
/// <param name="urlSettings">The default settings applied to every generated URL, or <see langword="null"/> for none.</param>
/// <exception cref="ArgumentNullException"><paramref name="discoverer"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
/// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery">WOPI discovery (v2)</seealso>
/// <seealso href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/adb48ba9-118a-43b6-82d7-9a508aad1583">MS-WOPI (v1)</seealso>
public partial class WopiUrlBuilder(
    IDiscoverer discoverer,
    ILogger<WopiUrlBuilder> logger,
    WopiUrlSettings? urlSettings = null)
{
    [GeneratedRegex("<(?<name>\\w*)=(?<value>\\w*)&*>")]
    private static partial Regex UrlParamRegex();

    private readonly IDiscoverer _wopiDiscoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
    private readonly ILogger<WopiUrlBuilder> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Gets the default URL settings applied to every generated URL, or <see langword="null"/> if none were supplied.
    /// </summary>
    public WopiUrlSettings? UrlSettings { get; } = urlSettings;

    /// <summary>
    /// Generates a WOPI client URL for a given file and action.
    /// </summary>
    /// <remarks>
    /// The <c>WOPI_SOURCE</c> placeholder is always resolved from <paramref name="wopiFileUrl"/>; when the template
    /// has no such placeholder, a <c>WOPISrc</c> query parameter is appended instead.
    /// </remarks>
    /// <param name="extension">The file extension used to select the URL template.</param>
    /// <param name="wopiFileUrl">The URL of the file's WOPI endpoint.</param>
    /// <param name="action">The action used to select the URL template.</param>
    /// <param name="urlSettings">Per-call URL settings, merged over <see cref="UrlSettings"/> (per-call values win per key).</param>
    /// <returns>The generated URL, or <see langword="null"/> if discovery has no URL template for the extension and action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wopiFileUrl"/> is <see langword="null"/>.</exception>
    public async Task<string?> GetFileUrlAsync(string extension, Uri wopiFileUrl, WopiActionEnum action, WopiUrlSettings? urlSettings = null)
    {
        ArgumentNullException.ThrowIfNull(wopiFileUrl);

        // Combine settings with method-arg precedence over constructor-arg.
        WopiUrlSettings combinedUrlSettings = [];
        if (UrlSettings is not null)
        {
            foreach (var kvp in UrlSettings) combinedUrlSettings[kvp.Key] = kvp.Value;
        }
        if (urlSettings is not null)
        {
            foreach (var kvp in urlSettings) combinedUrlSettings[kvp.Key] = kvp.Value;
        }

        // Single source of truth for the WopiSrc value: the wopiFileUrl parameter. The
        // WOPI_SOURCE placeholder, when present in a template, gets substituted with this
        // value (URL-escaped by ResolveOptionalParameter). Any caller-provided WOPI_SOURCE
        // value in urlSettings is overwritten so a URL never carries two different WopiSrc
        // values.
        combinedUrlSettings[WopiUrlSettings.Placeholders.WopiSource] = wopiFileUrl.ToString();

        var template = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action).ConfigureAwait(false);
        if (string.IsNullOrEmpty(template))
        {
            LogTemplateNotFound(_logger, extension, action);
            return null;
        }

        var templateHasWopiSourcePlaceholder = template.Contains(WopiUrlSettings.Placeholders.WopiSource, StringComparison.Ordinal);

        var url = UrlParamRegex().Replace(template, m => ResolveOptionalParameter(m.Groups["name"].Value, m.Groups["value"].Value, combinedUrlSettings) ?? string.Empty);
        url = url.TrimEnd('&');

        // Only append &WOPISrc= when the template did not already carry the WOPI_SOURCE
        // placeholder. Modern Office Online / M365 templates include `<wopisrc=WOPI_SOURCE&>`
        // and would otherwise produce two WopiSrc params (lowercase from the substitution,
        // uppercase from this append) with potentially different values.
        if (!templateHasWopiSourcePlaceholder)
        {
            url += "&WOPISrc=" + Uri.EscapeDataString(wopiFileUrl.ToString());
        }

        LogFileUrlGenerated(_logger, extension, action);
        return url;
    }

    private static string? ResolveOptionalParameter(string name, string value, WopiUrlSettings urlSettings)
    {
        if (urlSettings.TryGetValue(value, out var param))
        {
            return name + "=" + Uri.EscapeDataString(param) + "&";
        }
        return null;
    }
}
