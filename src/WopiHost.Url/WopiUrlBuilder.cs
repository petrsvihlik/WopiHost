using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Url;

/// <summary>
/// Generates WOPI URLs according to the specification
/// WOPI v2 spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery
/// WOPI v1 spec: https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/adb48ba9-118a-43b6-82d7-9a508aad1583
/// </summary>
/// <remarks>
/// Creates a new instance of WOPI URL generator class.
/// </remarks>
/// <param name="discoverer">Provider of WOPI discovery data.</param>
/// <param name="logger">Logger.</param>
/// <param name="urlSettings">Additional settings influencing behavior of the WOPI client.</param>
public partial class WopiUrlBuilder(
    IDiscoverer discoverer,
    ILogger<WopiUrlBuilder> logger,
    WopiUrlSettings? urlSettings = null)
{
    [GeneratedRegex("<(?<name>\\w*)=(?<value>\\w*)&*>")]
    private static partial Regex UrlParamRegex();

    private readonly IDiscoverer _wopiDiscoverer = discoverer;
    private readonly ILogger<WopiUrlBuilder> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Additional URL parameters influencing the behavior of the WOPI client.
    /// </summary>
    public WopiUrlSettings? UrlSettings { get; } = urlSettings;

    /// <summary>
    /// Generates an URL for a given file and action.
    /// </summary>
    /// <param name="extension">File extension used to identify a correct URL template.</param>
    /// <param name="wopiFileUrl">URL of a file.</param>
    /// <param name="action">Action used to identify a correct URL template.</param>
    /// <param name="urlSettings">Additional URL settings (if not specified, defaults passed to the class constructor will be used).</param>
    /// <returns></returns>
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
            foreach (var kvp in urlSettings) combinedUrlSettings[kvp.Key] = kvp.Value; // overrides
        }

        // Single source of truth for the WopiSrc value: the wopiFileUrl parameter. The
        // WOPI_SOURCE placeholder, when present in a template, gets substituted with this
        // value (URL-escaped by ResolveOptionalParameter). Any caller-provided WOPI_SOURCE
        // value in urlSettings is overwritten so we never produce two different WopiSrc
        // values in the same URL.
        combinedUrlSettings[WopiSourcePlaceholder] = wopiFileUrl.ToString();

        var template = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);
        if (string.IsNullOrEmpty(template))
        {
            LogTemplateNotFound(_logger, extension, action);
            return null;
        }

        var templateHasWopiSourcePlaceholder = template.Contains(WopiSourcePlaceholder, StringComparison.Ordinal);

        // Resolve optional parameters
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

    private const string WopiSourcePlaceholder = "WOPI_SOURCE";

    private static string? ResolveOptionalParameter(string name, string value, WopiUrlSettings urlSettings)
    {
        if (urlSettings.TryGetValue(value, out var param))
        {
            return name + "=" + Uri.EscapeDataString(param) + "&";
        }
        return null;
    }
}
