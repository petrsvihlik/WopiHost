﻿using System.Text.RegularExpressions;

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
/// <param name="urlSettings">Additional settings influencing behavior of the WOPI client.</param>
public partial class WopiUrlBuilder(IDiscoverer discoverer, WopiUrlSettings? urlSettings = null)
{
    [GeneratedRegex("<(?<name>\\w*)=(?<value>\\w*)&*>")]
    private static partial Regex UrlParamRegex();
    
    private readonly IDiscoverer _wopiDiscoverer = discoverer;

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
        var combinedUrlSettings = new WopiUrlSettings((urlSettings ?? []).Merge(UrlSettings ?? []));
        var template = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);
        if (!string.IsNullOrEmpty(template))
        {
            // Resolve optional parameters
            var url = UrlParamRegex().Replace(template, m => ResolveOptionalParameter(m.Groups["name"].Value, m.Groups["value"].Value, combinedUrlSettings) ?? string.Empty);
            url = url.TrimEnd('&');

            // Append mandatory parameters
            url += "&WOPISrc=" + Uri.EscapeDataString(wopiFileUrl.ToString());

            return url;
        }
        return null;
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
