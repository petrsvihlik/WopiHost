using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Discovery.Models;

namespace WopiHost.SmokeTests.Fixtures;

/// <summary>
/// Stand-in <see cref="IDiscoverer"/> that returns canned values so the sample frontend can
/// render without a real WOPI client to talk to. Reports a small whitelist of "supported"
/// extensions; everything else (including <c>.html</c> and <c>.wopitest</c> from the test
/// docs folder) is rejected so the smoke tests can assert disabled-icon styling on those rows.
/// </summary>
internal sealed class FakeDiscoverer : IDiscoverer
{
    /// <summary>Extensions for which both View and Edit are reported as supported.</summary>
    public static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "docx", "xlsx", "pptx" };

    public Task<bool> SupportsExtensionAsync(string extension) =>
        Task.FromResult(SupportedExtensions.Contains(extension.TrimStart('.')));

    public Task<bool> SupportsActionAsync(string extension, WopiActionEnum action) =>
        Task.FromResult(SupportedExtensions.Contains(extension.TrimStart('.')));

    public Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action) =>
        Task.FromResult<string?>($"https://office.example.test/{action.ToString().ToLowerInvariant()}/{extension}/edit?ui=&rs=&hid=&dchat=&showpagestat=");

    public Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action) =>
        Task.FromResult(Enumerable.Empty<string>());

    public Task<string?> GetApplicationNameAsync(string extension) =>
        Task.FromResult<string?>("FakeOffice");

    public Task<Uri?> GetApplicationFavIconAsync(string extension) =>
        Task.FromResult<Uri?>(new Uri("https://office.example.test/favicon.ico"));

    public Task<WopiProofKeys> GetProofKeysAsync() =>
        Task.FromResult(new WopiProofKeys());
}
