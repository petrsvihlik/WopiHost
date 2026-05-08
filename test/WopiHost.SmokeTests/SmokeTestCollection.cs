using WopiHost.SmokeTests.Fixtures;
using Xunit;

namespace WopiHost.SmokeTests;

/// <summary>
/// Marker for the xUnit collection that shares a single <see cref="PlaywrightFixture"/>
/// across all smoke-test classes — saves ~3-5s of browser startup per class.
/// </summary>
[CollectionDefinition(nameof(SmokeTestCollection))]
public sealed class SmokeTestCollection : ICollectionFixture<PlaywrightFixture>;
