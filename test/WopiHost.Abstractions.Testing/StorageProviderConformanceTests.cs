using Xunit;

namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Conformance suite every <see cref="IWopiStorageProvider"/> implementation must satisfy.
/// Derive a concrete sealed subclass in each provider's test project and override
/// <see cref="Factory"/>. xUnit discovers tests on base classes automatically, so the subclass
/// needs nothing beyond the property override.
/// </summary>
/// <remarks>
/// The suite drives behavior through the public interfaces only — it seeds via
/// <see cref="IWopiWritableStorageProvider"/> and asserts via <see cref="IWopiStorageProvider"/>,
/// with no reflection or backend-specific shaping. Each test gets a fresh, empty-rooted provider
/// from the factory and creates exactly the resources it asserts on. Provider-specific behavior
/// (blob-metadata edge cases, file-system path quirks) belongs in the provider's own test project.
/// </remarks>
public abstract class StorageProviderConformanceTests
{
    // Hoisted per CA1861 (avoid constant array arguments).
    private static readonly string[] s_docxFilter = [".docx"];

    /// <summary>Override to supply a factory that builds a fresh provider per test.</summary>
    protected abstract IStorageProviderTestFactory Factory { get; }

    private static async Task<List<IWopiFile>> ListFilesAsync(
        IWopiStorageProvider provider, string containerId, IReadOnlyCollection<string>? extensions = null)
    {
        var list = new List<IWopiFile>();
        await foreach (var file in provider.GetWopiFiles(containerId, extensions))
        {
            list.Add(file);
        }
        return list;
    }

    private static async Task<List<IWopiContainer>> ListContainersAsync(
        IWopiStorageProvider provider, string containerId)
    {
        var list = new List<IWopiContainer>();
        await foreach (var container in provider.GetWopiContainers(containerId))
        {
            list.Add(container);
        }
        return list;
    }

    [Fact]
    public async Task RootContainer_ResolvesByItsOwnIdentifier()
    {
        await using var ctx = await Factory.CreateAsync();
        var rootId = ctx.Reader.RootContainer.Identifier;

        var root = await ctx.Reader.GetWopiContainer(rootId);

        Assert.NotNull(root);
        Assert.Equal(rootId, root.Identifier);
    }

    [Fact]
    public async Task GetWopiFile_RoundTripsByIdentifier()
    {
        await using var ctx = await Factory.CreateAsync();
        var created = await ctx.Writer.CreateWopiChildFile(ctx.Reader.RootContainer.Identifier, "report.docx");
        Assert.NotNull(created);

        var fetched = await ctx.Reader.GetWopiFile(created.Identifier);

        Assert.NotNull(fetched);
        Assert.Equal(created.Identifier, fetched.Identifier);
    }

    [Fact]
    public async Task GetWopiContainer_RoundTripsByIdentifier()
    {
        await using var ctx = await Factory.CreateAsync();
        var created = await ctx.Writer.CreateWopiChildContainer(ctx.Reader.RootContainer.Identifier, "folder");
        Assert.NotNull(created);

        var fetched = await ctx.Reader.GetWopiContainer(created.Identifier);

        Assert.NotNull(fetched);
        Assert.Equal(created.Identifier, fetched.Identifier);
    }

    [Fact]
    public async Task GetWopiFiles_ReturnsChildFiles()
    {
        await using var ctx = await Factory.CreateAsync();
        var rootId = ctx.Reader.RootContainer.Identifier;
        var a = await ctx.Writer.CreateWopiChildFile(rootId, "a.docx");
        var b = await ctx.Writer.CreateWopiChildFile(rootId, "b.txt");
        Assert.NotNull(a);
        Assert.NotNull(b);

        var files = await ListFilesAsync(ctx.Reader, rootId);

        Assert.Contains(files, f => f.Identifier == a.Identifier);
        Assert.Contains(files, f => f.Identifier == b.Identifier);
    }

    [Fact]
    public async Task GetWopiFiles_ExtensionFilter_ReturnsOnlyMatching()
    {
        await using var ctx = await Factory.CreateAsync();
        var rootId = ctx.Reader.RootContainer.Identifier;
        var doc = await ctx.Writer.CreateWopiChildFile(rootId, "keep.docx");
        var txt = await ctx.Writer.CreateWopiChildFile(rootId, "skip.txt");
        Assert.NotNull(doc);
        Assert.NotNull(txt);

        var files = await ListFilesAsync(ctx.Reader, rootId, s_docxFilter);

        Assert.Contains(files, f => f.Identifier == doc.Identifier);
        Assert.DoesNotContain(files, f => f.Identifier == txt.Identifier);
    }

    [Fact]
    public async Task GetWopiContainers_ReturnsChildContainers()
    {
        await using var ctx = await Factory.CreateAsync();
        var rootId = ctx.Reader.RootContainer.Identifier;
        var folder = await ctx.Writer.CreateWopiChildContainer(rootId, "folder");
        Assert.NotNull(folder);

        var containers = await ListContainersAsync(ctx.Reader, rootId);

        Assert.Contains(containers, c => c.Identifier == folder.Identifier);
    }

    [Fact]
    public async Task GetFileAncestors_AreRootFirstAndIncludeParent()
    {
        await using var ctx = await Factory.CreateAsync();
        var rootId = ctx.Reader.RootContainer.Identifier;
        var folder = await ctx.Writer.CreateWopiChildContainer(rootId, "folder");
        Assert.NotNull(folder);
        var nested = await ctx.Writer.CreateWopiChildFile(folder.Identifier, "nested.txt");
        Assert.NotNull(nested);

        var ancestors = await ctx.Reader.GetFileAncestors(nested.Identifier);

        Assert.Equal(new[] { rootId, folder.Identifier }, ancestors.Select(a => a.Identifier));
    }

    [Fact]
    public async Task GetContainerAncestors_AreRootFirstExcludingSelf()
    {
        await using var ctx = await Factory.CreateAsync();
        var rootId = ctx.Reader.RootContainer.Identifier;
        var folder = await ctx.Writer.CreateWopiChildContainer(rootId, "folder");
        Assert.NotNull(folder);

        var ancestors = await ctx.Reader.GetContainerAncestors(folder.Identifier);

        Assert.Equal(new[] { rootId }, ancestors.Select(a => a.Identifier));
    }

    [Fact]
    public async Task GetContainerAncestors_OfRoot_IsEmpty()
    {
        await using var ctx = await Factory.CreateAsync();

        var ancestors = await ctx.Reader.GetContainerAncestors(ctx.Reader.RootContainer.Identifier);

        Assert.Empty(ancestors);
    }

    [Fact]
    public async Task GetWopiFile_UnknownId_ReturnsNull()
    {
        await using var ctx = await Factory.CreateAsync();
        Assert.Null(await ctx.Reader.GetWopiFile("does-not-exist"));
    }

    [Fact]
    public async Task GetWopiContainer_UnknownId_ReturnsNull()
    {
        await using var ctx = await Factory.CreateAsync();
        Assert.Null(await ctx.Reader.GetWopiContainer("does-not-exist"));
    }

    [Fact]
    public async Task GetWopiFileByName_ResolvesSeededFile()
    {
        await using var ctx = await Factory.CreateAsync();
        var rootId = ctx.Reader.RootContainer.Identifier;
        var created = await ctx.Writer.CreateWopiChildFile(rootId, "byname.docx");
        Assert.NotNull(created);

        var byName = await ctx.Reader.GetWopiFileByName(rootId, "byname.docx");

        Assert.NotNull(byName);
        Assert.Equal(created.Identifier, byName.Identifier);
    }

    [Fact]
    public async Task GetWopiFileByName_MissingParent_ReturnsNull()
    {
        await using var ctx = await Factory.CreateAsync();
        Assert.Null(await ctx.Reader.GetWopiFileByName("does-not-exist", "whatever.txt"));
    }

    [Fact]
    public async Task GetWopiContainerByName_Missing_ReturnsNull()
    {
        await using var ctx = await Factory.CreateAsync();
        var rootId = ctx.Reader.RootContainer.Identifier;
        Assert.Null(await ctx.Reader.GetWopiContainerByName(rootId, "no-such-folder"));
    }
}
