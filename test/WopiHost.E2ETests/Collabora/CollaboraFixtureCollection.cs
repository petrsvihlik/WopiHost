using WopiHost.E2ETests.Fixtures;

namespace WopiHost.E2ETests.Collabora;

/// <summary>
/// xUnit collection definition that pairs the Aspire-managed distributed application
/// (<see cref="CollaboraAppFixture"/>) with the Playwright browser
/// (<see cref="PlaywrightFixture"/>). Members of this collection share a single Collabora
/// container instance — that's where the ~15s cold-start cost is amortised.
/// <c>DisableParallelization</c> also keeps this collection from running concurrently with the
/// ONLYOFFICE one: each boots its own Aspire stack on the same pinned host ports.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CollaboraFixtureCollection
    : ICollectionFixture<CollaboraAppFixture>,
      ICollectionFixture<PlaywrightFixture>
{
    /// <summary>Collection name referenced by the suite's test classes.</summary>
    public const string Name = nameof(CollaboraFixtureCollection);
}
