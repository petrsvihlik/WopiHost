using WopiHost.SmokeTests.Fixtures;
using Xunit;

namespace WopiHost.SmokeTests;

/// <summary>
/// Defines the xUnit collection that shares a single <see cref="PlaywrightFixture"/> across all
/// smoke-test classes, so the browser starts once instead of per class.
/// </summary>
[CollectionDefinition(nameof(SmokeTestCollection))]
public sealed class SmokeTestCollection : ICollectionFixture<PlaywrightFixture>;
