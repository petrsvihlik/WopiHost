﻿using System.Xml.Linq;

namespace WopiHost.Discovery;

/// <summary>
/// A discovery file provider that loads the discovery file from a WOPI client over HTTP.
/// </summary>
public class HttpDiscoveryFileProvider : IDiscoveryFileProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates an instance of a discovery file provider that loads the discovery file from a WOPI client over HTTP.
    /// </summary>
    /// <param name="httpClient">An HTTP client with a <see cref="HttpClient.BaseAddress"/> configured to point to a WOPI client.</param>
    public HttpDiscoveryFileProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<XElement> GetDiscoveryXmlAsync()
    {
        try
        {
            var stream = await _httpClient.GetStreamAsync(new Uri("/hosting/discovery", UriKind.Relative));
            return XElement.Load(stream);
        }
        catch (HttpRequestException e)
        {
            throw new DiscoveryException($"There was a problem retrieving the discovery file. Please check availability of the WOPI Client at '{_httpClient.BaseAddress}'.", e);
        }
    }
}
