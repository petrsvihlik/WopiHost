using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Spins up a Navikt mock-oauth2-server Testcontainer for the duration of a test class collection.
/// Exposes the issuer URL once started, or marks itself unavailable if Docker is not running.
/// </summary>
/// <remarks>
/// Image: <c>ghcr.io/navikt/mock-oauth2-server</c>. The container's authorize endpoint
/// auto-issues a code (no UI), making the OIDC flow testable end-to-end without a browser.
/// </remarks>
public sealed class MockOidcServerFixture : IAsyncLifetime
{
    /// <summary>Issuer name used by the mock server. Discovery URL is <c>{BaseUrl}/{Issuer}/.well-known/openid-configuration</c>.</summary>
    public const string Issuer = "default";

    private const string Image = "ghcr.io/navikt/mock-oauth2-server:2.1.10";
    private const ushort ContainerPort = 8080;

    private IContainer? _container;

    /// <summary>True if Docker is available and the container started.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Reason the fixture is unavailable (for the skip message), or null if it is.</summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>Base URL of the running mock server (host-mapped). Null when <see cref="IsAvailable"/> is false.</summary>
    public Uri? BaseUrl { get; private set; }

    /// <summary>Authority URL passed to the OIDC client (issuer). Null when <see cref="IsAvailable"/> is false.</summary>
    public Uri? Authority => BaseUrl is null ? null : new Uri(BaseUrl, Issuer);

    public async Task InitializeAsync()
    {
        try
        {
            _container = new ContainerBuilder()
                .WithImage(Image)
                .WithPortBinding(ContainerPort, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r =>
                    r.ForPort(ContainerPort).ForPath($"/{Issuer}/.well-known/openid-configuration")))
                .Build();

            await _container.StartAsync();

            var mappedPort = _container.GetMappedPublicPort(ContainerPort);
            BaseUrl = new Uri($"http://{_container.Hostname}:{mappedPort}/");
            IsAvailable = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            IsAvailable = false;
            UnavailableReason = $"Docker / Testcontainers unavailable: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
