using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Discovery.Models;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Stand-in <see cref="IDiscoverer"/> that returns canned values, used by the OIDC sample's
/// <c>WebApplicationFactory</c> so tests don't reach out to real Office Online Server endpoints
/// (the configured ClientUrl points to an unreachable test hostname).
/// </summary>
internal sealed class FakeDiscoverer : IDiscoverer
{
    public Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action) =>
        Task.FromResult<string?>($"https://office.example.test/{action.ToString().ToLowerInvariant()}/{extension}/edit?ui=&rs=&hid=&dchat=&showpagestat=");

    public Task<bool> SupportsExtensionAsync(string extension) => Task.FromResult(true);

    public Task<bool> SupportsActionAsync(string extension, WopiActionEnum action) => Task.FromResult(true);

    public Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action) =>
        Task.FromResult(Enumerable.Empty<string>());

    public Task<bool> RequiresCobaltAsync(string extension, WopiActionEnum action) => Task.FromResult(false);

    public Task<string?> GetApplicationNameAsync(string extension) => Task.FromResult<string?>("FakeOffice");

    public Task<Uri?> GetApplicationFavIconAsync(string extension) =>
        Task.FromResult<Uri?>(new Uri("https://office.example.test/favicon.ico"));

    public Task<WopiProofKeys> GetProofKeysAsync() => Task.FromResult(new WopiProofKeys());
}
