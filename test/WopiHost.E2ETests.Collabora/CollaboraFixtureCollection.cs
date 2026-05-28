using WopiHost.E2ETests.Collabora.Fixtures;

namespace WopiHost.E2ETests.Collabora;

/// <summary>
/// xUnit collection definition that pairs the Aspire-managed distributed application
/// (<see cref="CollaboraAppFixture"/>) with the Playwright browser
/// (<see cref="PlaywrightFixture"/>). Members of this collection share a single Collabora
/// container instance — that's where the ~15s cold-start cost is amortised.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CollaboraFixtureCollection
    : ICollectionFixture<CollaboraAppFixture>,
      ICollectionFixture<PlaywrightFixture>
{
    public const string Name = nameof(CollaboraFixtureCollection);
}
