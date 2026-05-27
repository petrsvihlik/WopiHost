using System.Security.Cryptography;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Discovery.Models;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// <see cref="IDiscoverer"/> that hosts an in-process RSA key pair so the
/// <c>WopiHost.Core.Security.Authentication.WopiProofValidator</c> can verify signatures end-to-end in integration tests without
/// reaching out to a live Office Online Server discovery endpoint.
/// </summary>
/// <remarks>
/// Existence rationale: the production <see cref="IDiscoverer"/> reads proof keys from the WOPI
/// client's <c>discovery.xml</c>. Synthesising that XML to drive integration tests would couple
/// the suite to discovery parsing — a separate concern with its own unit coverage. Hosting the
/// keys here keeps the proof-validation integration tests focused on the proof gate itself.
/// </remarks>
internal sealed class FakeDiscovererWithProofKeys : IDiscoverer, IDisposable
{
    private readonly RSACryptoServiceProvider _currentKey;
    private readonly RSACryptoServiceProvider _oldKey;

    public FakeDiscovererWithProofKeys()
    {
        _currentKey = new RSACryptoServiceProvider(2048);
        _oldKey = new RSACryptoServiceProvider(2048);
    }

    /// <summary>RSA key whose CSP blob is published as <see cref="WopiProofKeys.Value"/>.</summary>
    public RSACryptoServiceProvider CurrentKey => _currentKey;

    /// <summary>RSA key whose CSP blob is published as <see cref="WopiProofKeys.OldValue"/>.</summary>
    public RSACryptoServiceProvider OldKey => _oldKey;

    public Task<WopiProofKeys> GetProofKeysAsync() => Task.FromResult(new WopiProofKeys
    {
        Value = Convert.ToBase64String(_currentKey.ExportCspBlob(false)),
        OldValue = Convert.ToBase64String(_oldKey.ExportCspBlob(false)),
    });

    public Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action) =>
        Task.FromResult<string?>($"https://office.example.test/{action.ToString().ToLowerInvariant()}/{extension}/edit?ui=&rs=&hid=&dchat=&showpagestat=");

    public Task<bool> SupportsExtensionAsync(string extension) => Task.FromResult(true);

    public Task<bool> SupportsActionAsync(string extension, WopiActionEnum action) => Task.FromResult(true);

    public Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action) =>
        Task.FromResult(Enumerable.Empty<string>());

    public Task<string?> GetApplicationNameAsync(string extension) => Task.FromResult<string?>("FakeOffice");

    public Task<Uri?> GetApplicationFavIconAsync(string extension) =>
        Task.FromResult<Uri?>(new Uri("https://office.example.test/favicon.ico"));

    public void Dispose()
    {
        _currentKey.Dispose();
        _oldKey.Dispose();
    }
}
