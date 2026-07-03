using FakeItEasy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider.Tests;

public class WopiFileSystemProviderTests : IDisposable
{
    // Filter arrays hoisted to static readonly to satisfy CA1861 (single allocation reused across
    // Theory iterations) and IDE0300 (collection-expression initializer).
    private static readonly string[] s_docxFilter = [".docx"];
    private static readonly string[] s_docxAndTxtFilter = [".docx", ".txt"];
    private static readonly string[] s_docxUpperFilter = [".DOCX"];

    private readonly DirectoryInfo _root;
    private readonly DirectoryInfo _sub;
    private readonly DirectoryInfo _empty;
    private readonly string _rootTxtPath;
    private readonly string _rootDocxPath;
    private readonly string _leafTxtPath;
    private readonly InMemoryFileIds _fileIds;
    private readonly IHostEnvironment _env;
    private readonly WopiFileSystemProvider _sut;

    public WopiFileSystemProviderTests()
    {
        _root = Directory.CreateTempSubdirectory("WopiFsTest_");
        _sub = _root.CreateSubdirectory("sub");
        _empty = _root.CreateSubdirectory("empty");
        _rootTxtPath = Path.Combine(_root.FullName, "root.txt");
        _rootDocxPath = Path.Combine(_root.FullName, "root.docx");
        _leafTxtPath = Path.Combine(_sub.FullName, "leaf.txt");
        File.WriteAllText(_rootTxtPath, "root-txt");
        File.WriteAllText(_rootDocxPath, "root-docx");
        File.WriteAllText(_leafTxtPath, "leaf");

        _fileIds = new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance);
        _env = A.Fake<IHostEnvironment>();
        A.CallTo(() => _env.ContentRootPath).Returns(_root.FullName);

        _sut = CreateProvider(_root.FullName);
    }

    public void Dispose()
    {
        _root.Refresh();
        if (_root.Exists) _root.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    private WopiFileSystemProvider CreateProvider(
        string rootPath,
        InMemoryFileIds? ids = null,
        IHostEnvironment? env = null)
    {
        var options = Options.Create(new WopiFileSystemProviderOptions { RootPath = rootPath });
        return new WopiFileSystemProvider(ids ?? _fileIds, env ?? _env, options, NullLogger<WopiFileSystemProvider>.Instance);
    }

    // ---------- Constructor ----------

    [Fact]
    public void Ctor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WopiFileSystemProvider(_fileIds, _env, options: null!, NullLogger<WopiFileSystemProvider>.Instance));
    }

    [Fact]
    public void Ctor_NullFileIds_Throws()
    {
        var options = Options.Create(new WopiFileSystemProviderOptions { RootPath = _root.FullName });

        Assert.Throws<ArgumentNullException>(() =>
            new WopiFileSystemProvider(fileIds: null!, _env, options, NullLogger<WopiFileSystemProvider>.Instance));
    }

    [Fact]
    public void Ctor_NullRootPath_Throws()
    {
        var options = Options.Create(new WopiFileSystemProviderOptions { RootPath = null! });
        Assert.ThrowsAny<Exception>(() =>
            new WopiFileSystemProvider(_fileIds, _env, options, NullLogger<WopiFileSystemProvider>.Instance));
    }

    [Fact]
    public void Ctor_RelativeRootPath_ResolvesAgainstContentRoot()
    {
        var ids = new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance);
        var env = A.Fake<IHostEnvironment>();
        A.CallTo(() => env.ContentRootPath).Returns(_root.FullName);

        var provider = CreateProvider(rootPath: "sub", ids, env);

        // The provider should now consider _sub as the root container.
        Assert.Equal(_sub.Name, provider.RootContainer.Name);
    }

    [Fact]
    public void Ctor_AbsoluteRootPath_UsedAsIs()
    {
        Assert.Equal(_root.Name, _sut.RootContainer.Name);
    }

    [Fact]
    public void Ctor_AlreadyScanned_SkipsRescan()
    {
        // Pre-populate the ids so WasScanned is true; ctor should skip ScanAll
        // but still find the manually-added root.
        var ids = new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance);
        ids.AddFile(_root.FullName);

        var provider = CreateProvider(_root.FullName, ids);
        Assert.NotNull(provider.RootContainer);
    }

    [Fact]
    public void Ctor_PrescannedWithoutRoot_ThrowsInvalidOperation()
    {
        // A pre-populated InMemoryFileIds that does NOT contain the root path.
        var ids = new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance);
        ids.AddFile(Path.Combine(_root.FullName, "unrelated"));

        Assert.Throws<InvalidOperationException>(() =>
            CreateProvider(_root.FullName, ids));
    }

    // ---------- GetWopiFile / GetWopiContainer ----------

    [Fact]
    public async Task GetWopiResource_ExistingFile_ReturnsWopiFile()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        var file = await _sut.GetWopiFile(fileId);

        Assert.NotNull(file);
        Assert.Equal(fileId, file.Identifier);
    }

    [Fact]
    public async Task GetWopiResource_ExistingFolder_ReturnsWopiFolder()
    {
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var folderId));

        var folder = await _sut.GetWopiContainer(folderId);

        Assert.NotNull(folder);
        Assert.Equal("sub", folder.Name);
    }

    [Fact]
    public async Task GetWopiResource_UnknownId_ReturnsNull()
    {
        var result = await _sut.GetWopiFile("does-not-exist");
        Assert.Null(result);
    }

    // ---------- GetWopiFiles ----------

    [Fact]
    public async Task GetWopiFiles_DefaultRoot_EnumeratesRootFiles()
    {
        var files = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles(_sut.RootContainer.Identifier))
        {
            files.Add(f);
        }
        Assert.Equal(2, files.Count); // root.txt + root.docx
    }

    [Fact]
    public async Task GetWopiFiles_WithSingleExtensionFilter_FiltersByExtension()
    {
        var files = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles(_sut.RootContainer.Identifier, s_docxFilter))
        {
            files.Add(f);
        }
        Assert.Single(files);
        Assert.Equal("root", files[0].Name);
        Assert.Equal("docx", files[0].Extension);
    }

    [Fact]
    public async Task GetWopiFiles_WithMultipleExtensionFilter_ReturnsUnionOfExtensions()
    {
        // The fixture writes root.txt + root.docx; both should come back when both extensions
        // are requested. Confirms the SelectMany-over-extensions plumbing emits disjoint
        // result sets without dropping any.
        var files = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles(_sut.RootContainer.Identifier, s_docxAndTxtFilter))
        {
            files.Add(f);
        }
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.Extension == "docx");
        Assert.Contains(files, f => f.Extension == "txt");
    }

    [Fact]
    public async Task GetWopiFiles_WithExtensionFilter_IsCaseInsensitive()
    {
        // WOPI spec mandates case-insensitive extension matching. The provider enforces this
        // explicitly via EnumerationOptions.MatchCasing — without it, Linux hosts would
        // case-sensitively miss a request for ".DOCX" against a "root.docx" file.
        var files = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles(_sut.RootContainer.Identifier, s_docxUpperFilter))
        {
            files.Add(f);
        }
        Assert.Single(files);
        Assert.Equal("docx", files[0].Extension);
    }

    [Fact]
    public async Task GetWopiFiles_WithEmptyExtensionFilter_ReturnsAllFiles()
    {
        // Per the contract: null OR empty = no filter.
        var withNull = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles(_sut.RootContainer.Identifier, fileExtensions: null))
        {
            withNull.Add(f);
        }
        var withEmpty = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles(_sut.RootContainer.Identifier, []))
        {
            withEmpty.Add(f);
        }
        Assert.Equal(2, withNull.Count); // root.txt + root.docx
        Assert.Equal(2, withEmpty.Count);
    }

    [Fact]
    public async Task GetWopiFiles_WithSubfolder_EnumeratesSubfolderFiles()
    {
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var subId));
        var files = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles(subId))
        {
            files.Add(f);
        }
        Assert.Single(files);
        Assert.Equal("leaf", files[0].Name);
    }

    [Fact]
    public async Task GetWopiFiles_UnknownContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
        {
            await foreach (var _ in _sut.GetWopiFiles("missing-id")) { }
        });
    }

    // ---------- GetWopiContainers ----------

    [Fact]
    public async Task GetWopiContainers_DefaultRoot_EnumeratesSubfolders()
    {
        var containers = new List<IWopiContainer>();
        await foreach (var c in _sut.GetWopiContainers(_sut.RootContainer.Identifier))
        {
            containers.Add(c);
        }

        Assert.Equal(2, containers.Count);
        Assert.Contains(containers, c => c.Name == "sub");
        Assert.Contains(containers, c => c.Name == "empty");
    }

    [Fact]
    public async Task GetWopiContainers_UnknownContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
        {
            await foreach (var _ in _sut.GetWopiContainers("missing-id")) { }
        });
    }

    // ---------- GetFileAncestors / GetContainerAncestors ----------

    [Fact]
    public async Task GetAncestors_FolderUnderRoot_ReturnsRootAncestor()
    {
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var subId));

        var ancestors = await _sut.GetContainerAncestors(subId);

        Assert.Single(ancestors);
        Assert.Equal(_root.Name, ancestors[0].Name);
    }

    [Fact]
    public async Task GetAncestors_RootFolder_ReturnsEmpty()
    {
        var ancestors = await _sut.GetContainerAncestors(_sut.RootContainer.Identifier);
        Assert.Empty(ancestors);
    }

    [Fact]
    public async Task GetAncestors_FileInRoot_ReturnsRoot()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        var ancestors = await _sut.GetFileAncestors(fileId);

        Assert.Single(ancestors);
        Assert.Equal(_root.Name, ancestors[0].Name);
    }

    [Fact]
    public async Task GetAncestors_FileInSubfolder_ReturnsRootThenImmediateParent()
    {
        Assert.True(_fileIds.TryGetFileId(_leafTxtPath, out var fileId));

        var ancestors = await _sut.GetFileAncestors(fileId);

        Assert.Equal(2, ancestors.Count);
        Assert.Equal(_root.Name, ancestors[0].Name);
        Assert.Equal("sub", ancestors[1].Name);
    }

    [Fact]
    public async Task GetAncestors_FileWithUnknownId_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _sut.GetFileAncestors("missing-id"));
    }

    [Fact]
    public async Task GetAncestors_FolderWithUnknownId_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.GetContainerAncestors("missing-id"));
    }

    // ---------- GetWopiFileByName / GetWopiContainerByName ----------

    [Fact]
    public async Task GetWopiResourceByName_File_ReturnsFile()
    {
        var rootId = _sut.RootContainer.Identifier;

        var file = await _sut.GetWopiFileByName(rootId, "root.txt");

        Assert.NotNull(file);
        Assert.Equal("root", file.Name);
    }

    [Fact]
    public async Task GetWopiResourceByName_Folder_ReturnsFolder()
    {
        var rootId = _sut.RootContainer.Identifier;

        var folder = await _sut.GetWopiContainerByName(rootId, "sub");

        Assert.NotNull(folder);
        Assert.Equal("sub", folder.Name);
    }

    [Fact]
    public async Task GetWopiResourceByName_MissingContainer_ReturnsNull()
    {
        // Aligned with WopiAzureStorageProvider's behaviour: the interface mandates null on a
        // missing parent.
        var result = await _sut.GetWopiFileByName("missing-id", "root.txt");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWopiResourceByName_MissingName_ReturnsNull()
    {
        var rootId = _sut.RootContainer.Identifier;

        var result = await _sut.GetWopiFileByName(rootId, "no-such-file.txt");

        Assert.Null(result);
    }

    // ---------- CheckValidFileName / CheckValidContainerName / FileNameMaxLength ----------

    [Fact]
    public void FileNameMaxLength_Is250() => Assert.Equal(250, _sut.FileNameMaxLength);

    [Fact]
    public async Task CheckValidName_ValidFileName_ReturnsTrue()
    {
        Assert.True(await _sut.CheckValidFileName("doc.txt"));
    }

    [Fact]
    public async Task CheckValidName_FileNameTooLong_ReturnsFalse()
    {
        var longName = new string('a', 251);
        Assert.False(await _sut.CheckValidFileName(longName));
    }

    [Fact]
    public async Task CheckValidName_FileNameAtMaxLength_ReturnsTrue()
    {
        // Documented contract is "length up to FileNameMaxLength" — inclusive. A name of exactly
        // FileNameMaxLength chars must be accepted, matching the Azure provider.
        var atLimit = new string('a', _sut.FileNameMaxLength);
        Assert.True(await _sut.CheckValidFileName(atLimit));
    }

    [Fact]
    public async Task CheckValidName_FileNameWithInvalidChar_ReturnsFalse()
    {
        Assert.False(await _sut.CheckValidFileName("bad\0name.txt"));
    }

    [Fact]
    public async Task CheckValidName_FolderName_ReturnsTrue()
    {
        Assert.True(await _sut.CheckValidContainerName("subdir"));
    }

    [Fact]
    public async Task CheckValidName_FolderNameWithInvalidChar_ReturnsFalse()
    {
        Assert.False(await _sut.CheckValidContainerName("bad\0path"));
    }

    [Theory]
    [InlineData("sub/sub")]      // `/` is in GetInvalidFileNameChars on both Windows and POSIX
    [InlineData(".")]
    [InlineData("..")]
    public async Task CheckValidName_FolderNameWithPathSeparatorOrNav_ReturnsFalse(string name)
    {
        // CheckValidContainerName must reject path separators: Path.GetInvalidPathChars() omits
        // the separators GetInvalidFileNameChars forbids, so a container name containing a path
        // separator would otherwise pass validation and silently break the storage layer. This
        // does not assert on `"foo\\bar"`: on Linux, `\` is a legal filename character (not a
        // separator) and the FS provider correctly follows the OS — the OS-agnostic backslash
        // rejection lives in the Azure provider instead.
        Assert.False(await _sut.CheckValidContainerName(name));
    }

    [Fact]
    public async Task CheckValidName_FolderNameTooLong_ReturnsFalse()
    {
        // CheckValidContainerName must cap length; without a cap a 10K-char container name passes.
        var longName = new string('a', _sut.FileNameMaxLength + 1);
        Assert.False(await _sut.CheckValidContainerName(longName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckValidName_EmptyOrWhitespace_ReturnsFalse(string name)
    {
        Assert.False(await _sut.CheckValidFileName(name));
        Assert.False(await _sut.CheckValidContainerName(name));
    }

    // ---------- GetSuggestedFileName / GetSuggestedContainerName ----------

    [Fact]
    public async Task GetSuggestedName_InvalidName_Throws()
    {
        var rootId = _sut.RootContainer.Identifier;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetSuggestedFileName(rootId, "bad\0name.txt"));
    }

    [Fact]
    public async Task GetSuggestedName_MissingContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.GetSuggestedFileName("missing", "doc.txt"));
    }

    [Fact]
    public async Task GetSuggestedName_FolderNoCollision_ReturnsName()
    {
        var rootId = _sut.RootContainer.Identifier;
        var name = await _sut.GetSuggestedContainerName(rootId, "fresh");
        Assert.Equal("fresh", name);
    }

    [Fact]
    public async Task GetSuggestedName_FolderCollision_AppendsCounter()
    {
        var rootId = _sut.RootContainer.Identifier;
        // "sub" already exists in fixture
        var name = await _sut.GetSuggestedContainerName(rootId, "sub");
        Assert.Equal("sub (1)", name);
    }

    [Fact]
    public async Task GetSuggestedName_FileNoCollision_ReturnsName()
    {
        var rootId = _sut.RootContainer.Identifier;
        var name = await _sut.GetSuggestedFileName(rootId, "fresh.txt");
        Assert.Equal("fresh.txt", name);
    }

    [Fact]
    public async Task GetSuggestedName_FileCollision_AppendsCounter()
    {
        var rootId = _sut.RootContainer.Identifier;
        // "root.txt" already exists in fixture
        var name = await _sut.GetSuggestedFileName(rootId, "root.txt");
        Assert.Equal("root (1).txt", name);
    }

    // ---------- CreateWopiChildFile / CreateWopiChildContainer ----------

    [Fact]
    public async Task CreateWopiChildResource_File_CreatesAndReturnsFile()
    {
        var rootId = _sut.RootContainer.Identifier;

        var file = await _sut.CreateWopiChildFile(rootId, "new.txt");

        Assert.NotNull(file);
        Assert.True(File.Exists(Path.Combine(_root.FullName, "new.txt")));
    }

    [Fact]
    public async Task CreateWopiChildResource_FileAtRoot_CreatesFileInRoot()
    {
        var file = await _sut.CreateWopiChildFile(_sut.RootContainer.Identifier, "rootless.txt");

        Assert.NotNull(file);
        Assert.True(File.Exists(Path.Combine(_root.FullName, "rootless.txt")));
    }

    [Fact]
    public async Task CreateWopiChildResource_FileAlreadyExists_Throws()
    {
        var rootId = _sut.RootContainer.Identifier;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateWopiChildFile(rootId, "root.txt"));
    }

    [Fact]
    public async Task CreateWopiChildResource_FileMissingContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.CreateWopiChildFile("missing", "x.txt"));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("sub/escape.txt")]
    [InlineData("..")]
    public async Task CreateWopiChildResource_FileWithTraversalName_Throws(string name)
    {
        // The name is client-controlled (relative/suggested target); a name that isn't a single
        // path segment must be rejected before Path.Combine so it can't escape the root.
        var rootId = _sut.RootContainer.Identifier;
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateWopiChildFile(rootId, name));
    }

    [Fact]
    public async Task CreateWopiChildResource_Folder_CreatesAndReturnsFolder()
    {
        var rootId = _sut.RootContainer.Identifier;

        var folder = await _sut.CreateWopiChildContainer(rootId, "new-folder");

        Assert.NotNull(folder);
        Assert.True(Directory.Exists(Path.Combine(_root.FullName, "new-folder")));
    }

    [Fact]
    public async Task CreateWopiChildResource_FolderAlreadyExists_Throws()
    {
        var rootId = _sut.RootContainer.Identifier;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateWopiChildContainer(rootId, "sub"));
    }

    [Fact]
    public async Task CreateWopiChildResource_FolderMissingContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.CreateWopiChildContainer("missing", "x"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("sub/escape")]
    [InlineData("..")]
    public async Task CreateWopiChildResource_FolderWithTraversalName_Throws(string name)
    {
        var rootId = _sut.RootContainer.Identifier;
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateWopiChildContainer(rootId, name));
    }

    // ---------- DeleteWopiFile / DeleteWopiContainer ----------

    [Fact]
    public async Task DeleteWopiResource_ExistingFile_DeletesAndReturnsTrue()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        var ok = await _sut.DeleteWopiFile(fileId);

        Assert.True(ok);
        Assert.False(File.Exists(_rootTxtPath));
    }

    [Fact]
    public async Task DeleteWopiResource_FileMissingId_ReturnsFalse()
    {
        // Return false for a missing identifier, matching WopiAzureStorageProvider and letting
        // the controller map cleanly to 404.
        var ok = await _sut.DeleteWopiFile("missing");

        Assert.False(ok);
    }

    [Fact]
    public async Task DeleteWopiResource_FileIdMappedButFileDeleted_ReturnsFalse()
    {
        // Edge case: the id-map still knows the path but the underlying file was deleted
        // out-of-band. Treat the same as a missing id — return false rather than throwing.
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));
        File.Delete(_rootTxtPath);

        var ok = await _sut.DeleteWopiFile(fileId);

        Assert.False(ok);
    }

    [Fact]
    public async Task DeleteWopiResource_EmptyFolder_DeletesAndReturnsTrue()
    {
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));

        var ok = await _sut.DeleteWopiContainer(folderId);

        Assert.True(ok);
        Assert.False(Directory.Exists(_empty.FullName));
    }

    [Fact]
    public async Task DeleteWopiResource_NonEmptyFolder_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var folderId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.DeleteWopiContainer(folderId));
    }

    [Fact]
    public async Task DeleteWopiResource_FolderMissingId_ReturnsFalse()
    {
        // Missing identifier returns false, matching WopiAzureStorageProvider.
        var ok = await _sut.DeleteWopiContainer("missing");

        Assert.False(ok);
    }

    [Fact]
    public async Task DeleteWopiResource_FolderIdMappedButDirGone_ReturnsFalse()
    {
        // Same as the file variant: id-map stale, treat as missing.
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));
        _empty.Delete(recursive: true);

        var ok = await _sut.DeleteWopiContainer(folderId);

        Assert.False(ok);
    }

    // ---------- RenameWopiFile / RenameWopiContainer ----------

    [Fact]
    public async Task RenameWopiResource_InvalidName_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.RenameWopiFile(fileId, "bad\0name.txt"));
    }

    [Fact]
    public async Task RenameWopiResource_File_RenamesAndReturnsTrue()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        var ok = await _sut.RenameWopiFile(fileId, "renamed.txt");

        Assert.True(ok);
        Assert.False(File.Exists(_rootTxtPath));
        Assert.True(File.Exists(Path.Combine(_root.FullName, "renamed.txt")));
    }

    [Fact]
    public async Task RenameWopiResource_FileMissingId_ReturnsFalse()
    {
        var ok = await _sut.RenameWopiFile("missing", "x.txt");

        Assert.False(ok);
    }

    [Fact]
    public async Task RenameWopiResource_FileTargetExists_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.RenameWopiFile(fileId, "root.docx"));
    }

    [Fact]
    public async Task RenameWopiResource_Folder_RenamesAndReturnsTrue()
    {
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));

        var ok = await _sut.RenameWopiContainer(folderId, "renamed");

        Assert.True(ok);
        Assert.False(Directory.Exists(_empty.FullName));
        Assert.True(Directory.Exists(Path.Combine(_root.FullName, "renamed")));
    }

    [Fact]
    public async Task RenameWopiResource_FolderMissingId_ReturnsFalse()
    {
        var ok = await _sut.RenameWopiContainer("missing", "x");

        Assert.False(ok);
    }

    [Fact]
    public async Task RenameWopiResource_FolderTargetExists_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.RenameWopiContainer(folderId, "sub"));
    }

    [Fact]
    public async Task RenameWopiContainer_InvalidName_Throws()
    {
        // Container variant of RenameWopiResource_InvalidName_Throws — exercises the container's
        // invalid-name guard (ArgumentException), which the file-path test doesn't cover.
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.RenameWopiContainer(folderId, "bad\0name"));
    }

    [Fact]
    public async Task GetSuggestedContainerName_InvalidName_Throws()
    {
        // The file-name variant is tested via GetSuggestedName_InvalidName_Throws; the
        // container path uses CheckValidContainerName instead.
        Assert.True(_fileIds.TryGetFileId(_root.FullName, out var rootId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetSuggestedContainerName(rootId, "bad\0name"));
    }

    [Fact]
    public async Task GetWritableFile_UnknownId_ReturnsNull()
    {
        // GetWritableFile is the writable-side counterpart of GetWopiFile; on miss it must
        // return null (not throw) so PutRelativeFile can map to 404.
        var result = await _sut.GetWritableFile("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWopiContainerByName_UnknownContainer_ReturnsNull()
    {
        // Mirrors GetWopiFileByName's missing-container behavior — the parent-container miss
        // returns null per the null-on-missing contract.
        var result = await _sut.GetWopiContainerByName("missing-container-id", "anything");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWopiContainerByName_UnknownName_ReturnsNull()
    {
        Assert.True(_fileIds.TryGetFileId(_root.FullName, out var rootId));

        var result = await _sut.GetWopiContainerByName(rootId, "does-not-exist");

        Assert.Null(result);
    }

    // ---------- Cross-process staleness ----------
    // The sample topology runs several processes over the same tree, each with its own id map
    // built at startup. A rename performed by one process must not make the file invisible or
    // unresolvable in the others.

    [Fact]
    public async Task GetWopiFiles_IncludesFileRenamedByAnotherProcess()
    {
        // A peer provider with its own map (a separate process in the sample topology)
        // performs the rename; this provider's startup map has never seen the new path.
        var peerIds = new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance);
        var peer = CreateProvider(_root.FullName, peerIds);
        Assert.True(peerIds.TryGetFileId(_rootTxtPath, out var peerFileId));
        Assert.True(await peer.RenameWopiFile(peerFileId, "renamed.txt"));

        var names = new List<string>();
        await foreach (var file in _sut.GetWopiFiles(_sut.RootContainer.Identifier))
        {
            names.Add($"{file.Name}.{file.Extension}");
        }

        Assert.Contains("renamed.txt", names);
        Assert.DoesNotContain("root.txt", names);
    }

    [Fact]
    public async Task GetWopiFile_ResolvesIdMintedByAnotherProcessAfterRename()
    {
        // This provider renames (retaining the original id per WOPI); a peer process derives
        // the id from the new path. Both ids must resolve here — the original keeps the live
        // editing session working, the peer-derived one serves clicks on a fresh listing.
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var originalId));
        Assert.True(await _sut.RenameWopiFile(originalId, "renamed.txt"));
        var newPath = Path.Combine(_root.FullName, "renamed.txt");
        var peerDerivedId = WopiResourceId.FromCanonicalPath(Path.GetFullPath(newPath).ToUpperInvariant());

        var viaPeerId = await _sut.GetWopiFile(peerDerivedId);
        var viaOriginalId = await _sut.GetWopiFile(originalId);

        Assert.NotNull(viaPeerId);
        Assert.Equal("renamed", viaPeerId.Name);
        Assert.NotNull(viaOriginalId);
        Assert.Equal("renamed", viaOriginalId.Name);
    }

    [Fact]
    public async Task GetWopiFiles_ExternalRename_YieldsSingleEntryForTheFile()
    {
        // File.Move outside any provider (a user shuffling files in Explorer). The stale map
        // entry must not produce a phantom listing row next to the lazily-registered one.
        var newPath = Path.Combine(_root.FullName, "moved.txt");
        File.Move(_rootTxtPath, newPath);

        var names = new List<string>();
        await foreach (var file in _sut.GetWopiFiles(_sut.RootContainer.Identifier))
        {
            names.Add($"{file.Name}.{file.Extension}");
        }

        Assert.Single(names, "moved.txt");
        Assert.DoesNotContain("root.txt", names);
    }

    [Fact]
    public async Task GetWopiContainers_IncludesFolderCreatedByAnotherProcess()
    {
        _root.CreateSubdirectory("late-folder");

        var names = new List<string>();
        await foreach (var container in _sut.GetWopiContainers(_sut.RootContainer.Identifier))
        {
            names.Add(container.Name);
        }

        Assert.Contains("late-folder", names);
    }

    [Fact]
    public async Task GetWopiFileByName_TraversalName_ReturnsNull()
    {
        // root.txt exists one level above sub and would resolve on disk via "..". Lazy
        // registration must not let a traversal name escape the container.
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var subId));

        var result = await _sut.GetWopiFileByName(subId, Path.Combine("..", "root.txt"));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWopiFileByName_RootedName_ReturnsNull()
    {
        // A rooted name would make Path.Combine discard the container path entirely and serve
        // an arbitrary on-disk file.
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var subId));

        var result = await _sut.GetWopiFileByName(subId, _rootDocxPath);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWopiContainerByName_TraversalOrRootedName_ReturnsNull()
    {
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var subId));

        Assert.Null(await _sut.GetWopiContainerByName(subId, ".."));
        Assert.Null(await _sut.GetWopiContainerByName(subId, Path.Combine("..", "empty")));
        Assert.Null(await _sut.GetWopiContainerByName(subId, _empty.FullName));
    }
}
