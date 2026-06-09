using WopiHost.E2ETests.OnlyOffice.Fixtures;

namespace WopiHost.E2ETests.OnlyOffice;

/// <summary>
/// xUnit collection definition that pairs the Aspire-managed distributed application
/// (<see cref="OnlyOfficeAppFixture"/>) with the Playwright browser
/// (<see cref="PlaywrightFixture"/>). Members of this collection share a single ONLYOFFICE
/// container instance — that's where the cold-start cost is amortised.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OnlyOfficeFixtureCollection
    : ICollectionFixture<OnlyOfficeAppFixture>,
      ICollectionFixture<PlaywrightFixture>
{
    public const string Name = nameof(OnlyOfficeFixtureCollection);
}
