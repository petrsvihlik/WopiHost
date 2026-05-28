using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// Shares a single <see cref="MockOidcServerFixture"/> across all Docker-backed test classes
/// so the container starts once for the whole assembly run.
/// </summary>
[CollectionDefinition(nameof(MockOidcCollection))]
public class MockOidcCollection : ICollectionFixture<MockOidcServerFixture>
{
}
