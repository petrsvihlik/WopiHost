using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider.Tests;

public class WopiFileSystemProviderTests : IDisposable
{
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
    }

    private WopiFileSystemProvider CreateProvider(
        string rootPath,
        InMemoryFileIds? ids = null,
        IHostEnvironment? env = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{WopiConfigurationSections.STORAGE_OPTIONS}:RootPath"] = rootPath,
            })
            .Build();
        return new WopiFileSystemProvider(ids ?? _fileIds, env ?? _env, config);
    }

    // ---------- Constructor ----------

    [Fact]
    public void Ctor_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WopiFileSystemProvider(_fileIds, _env, configuration: null!));
    }

    [Fact]
    public void Ctor_NullFileIds_Throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{WopiConfigurationSections.STORAGE_OPTIONS}:RootPath"] = _root.FullName,
            }).Build();

        Assert.Throws<ArgumentNullException>(() =>
            new WopiFileSystemProvider(fileIds: null!, _env, config));
    }

    [Fact]
    public void Ctor_MissingStorageOptionsSection_Throws()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        Assert.ThrowsAny<Exception>(() =>
            new WopiFileSystemProvider(_fileIds, _env, emptyConfig));
    }

    [Fact]
    public void Ctor_RelativeRootPath_ResolvesAgainstContentRoot()
    {
        var ids = new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance);
        var env = A.Fake<IHostEnvironment>();
        A.CallTo(() => env.ContentRootPath).Returns(_root.FullName);

        var provider = CreateProvider(rootPath: "sub", ids, env);

        // The provider should now consider _sub as the root container.
        Assert.Equal(_sub.Name, provider.RootContainerPointer.Name);
    }

    [Fact]
    public void Ctor_AbsoluteRootPath_UsedAsIs()
    {
        Assert.Equal(_root.Name, _sut.RootContainerPointer.Name);
    }

    [Fact]
    public void Ctor_AlreadyScanned_SkipsRescan()
    {
        // Pre-populate the ids so WasScanned is true; ctor should skip ScanAll
        // but still find the root because we add it manually.
        var ids = new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance);
        ids.AddFile(_root.FullName);

        var provider = CreateProvider(_root.FullName, ids);
        Assert.NotNull(provider.RootContainerPointer);
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

    // ---------- GetWopiResource<T> ----------

    [Fact]
    public async Task GetWopiResource_ExistingFile_ReturnsWopiFile()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        var file = await _sut.GetWopiResource<IWopiFile>(fileId);

        Assert.NotNull(file);
        Assert.Equal(fileId, file.Identifier);
    }

    [Fact]
    public async Task GetWopiResource_ExistingFolder_ReturnsWopiFolder()
    {
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var folderId));

        var folder = await _sut.GetWopiResource<IWopiFolder>(folderId);

        Assert.NotNull(folder);
        Assert.Equal("sub", folder.Name);
    }

    [Fact]
    public async Task GetWopiResource_UnsupportedType_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _sut.GetWopiResource<IWopiResource>(fileId));
    }

    [Fact]
    public async Task GetWopiResource_UnknownId_ReturnsNull()
    {
        var result = await _sut.GetWopiResource<IWopiFile>("does-not-exist");
        Assert.Null(result);
    }

    // ---------- GetWopiFiles ----------

    [Fact]
    public async Task GetWopiFiles_DefaultRoot_EnumeratesRootFiles()
    {
        var files = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles())
        {
            files.Add(f);
        }
        Assert.Equal(2, files.Count); // root.txt + root.docx
    }

    [Fact]
    public async Task GetWopiFiles_WithSearchPattern_FiltersByPattern()
    {
        var files = new List<IWopiFile>();
        await foreach (var f in _sut.GetWopiFiles(searchPattern: "*.docx"))
        {
            files.Add(f);
        }
        Assert.Single(files);
        Assert.Equal("root", files[0].Name);
        Assert.Equal("docx", files[0].Extension);
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
        var containers = new List<IWopiFolder>();
        await foreach (var c in _sut.GetWopiContainers())
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

    // ---------- GetAncestors<T> ----------

    [Fact]
    public async Task GetAncestors_FolderUnderRoot_ReturnsRootAncestor()
    {
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var subId));

        var ancestors = await _sut.GetAncestors<IWopiFolder>(subId);

        Assert.Single(ancestors);
        Assert.Equal(_root.Name, ancestors[0].Name);
    }

    [Fact]
    public async Task GetAncestors_RootFolder_ReturnsEmpty()
    {
        var ancestors = await _sut.GetAncestors<IWopiFolder>(_sut.RootContainerPointer.Identifier);
        Assert.Empty(ancestors);
    }

    [Fact]
    public async Task GetAncestors_FileInRoot_ReturnsRoot()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        var ancestors = await _sut.GetAncestors<IWopiFile>(fileId);

        Assert.Single(ancestors);
        Assert.Equal(_root.Name, ancestors[0].Name);
    }

    [Fact]
    public async Task GetAncestors_FileInSubfolder_ReturnsRootThenImmediateParent()
    {
        Assert.True(_fileIds.TryGetFileId(_leafTxtPath, out var fileId));

        var ancestors = await _sut.GetAncestors<IWopiFile>(fileId);

        Assert.Equal(2, ancestors.Count);
        Assert.Equal(_root.Name, ancestors[0].Name);
        Assert.Equal("sub", ancestors[1].Name);
    }

    [Fact]
    public async Task GetAncestors_FileWithUnknownId_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _sut.GetAncestors<IWopiFile>("missing-id"));
    }

    [Fact]
    public async Task GetAncestors_FolderWithUnknownId_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.GetAncestors<IWopiFolder>("missing-id"));
    }

    // ---------- GetWopiResourceByName<T> ----------

    [Fact]
    public async Task GetWopiResourceByName_File_ReturnsFile()
    {
        var rootId = _sut.RootContainerPointer.Identifier;

        var file = await _sut.GetWopiResourceByName<IWopiFile>(rootId, "root.txt");

        Assert.NotNull(file);
        Assert.Equal("root", file.Name);
    }

    [Fact]
    public async Task GetWopiResourceByName_Folder_ReturnsFolder()
    {
        var rootId = _sut.RootContainerPointer.Identifier;

        var folder = await _sut.GetWopiResourceByName<IWopiFolder>(rootId, "sub");

        Assert.NotNull(folder);
        Assert.Equal("sub", folder.Name);
    }

    [Fact]
    public async Task GetWopiResourceByName_MissingContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.GetWopiResourceByName<IWopiFile>("missing-id", "root.txt"));
    }

    [Fact]
    public async Task GetWopiResourceByName_MissingName_ReturnsNull()
    {
        var rootId = _sut.RootContainerPointer.Identifier;

        var result = await _sut.GetWopiResourceByName<IWopiFile>(rootId, "no-such-file.txt");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWopiResourceByName_UnsupportedType_Throws()
    {
        var rootId = _sut.RootContainerPointer.Identifier;

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _sut.GetWopiResourceByName<IWopiResource>(rootId, "root.txt"));
    }

    // ---------- CheckValidName / FileNameMaxLength ----------

    [Fact]
    public void FileNameMaxLength_Is250() => Assert.Equal(250, _sut.FileNameMaxLength);

    [Fact]
    public async Task CheckValidName_ValidFileName_ReturnsTrue()
    {
        Assert.True(await _sut.CheckValidName<IWopiFile>("doc.txt"));
    }

    [Fact]
    public async Task CheckValidName_FileNameTooLong_ReturnsFalse()
    {
        var longName = new string('a', 251);
        Assert.False(await _sut.CheckValidName<IWopiFile>(longName));
    }

    [Fact]
    public async Task CheckValidName_FileNameWithInvalidChar_ReturnsFalse()
    {
        Assert.False(await _sut.CheckValidName<IWopiFile>("bad\0name.txt"));
    }

    [Fact]
    public async Task CheckValidName_FolderName_ReturnsTrue()
    {
        Assert.True(await _sut.CheckValidName<IWopiFolder>("subdir"));
    }

    [Fact]
    public async Task CheckValidName_FolderNameWithInvalidChar_ReturnsFalse()
    {
        Assert.False(await _sut.CheckValidName<IWopiFolder>("bad\0path"));
    }

    [Fact]
    public async Task CheckValidName_UnsupportedType_Throws()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _sut.CheckValidName<IWopiResource>("any"));
    }

    // ---------- GetSuggestedName ----------

    [Fact]
    public async Task GetSuggestedName_InvalidName_Throws()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetSuggestedName<IWopiFile>(rootId, "bad\0name.txt"));
    }

    [Fact]
    public async Task GetSuggestedName_MissingContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.GetSuggestedName<IWopiFile>("missing", "doc.txt"));
    }

    [Fact]
    public async Task GetSuggestedName_FolderNoCollision_ReturnsName()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        var name = await _sut.GetSuggestedName<IWopiFolder>(rootId, "fresh");
        Assert.Equal("fresh", name);
    }

    [Fact]
    public async Task GetSuggestedName_FolderCollision_AppendsCounter()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        // "sub" already exists in fixture
        var name = await _sut.GetSuggestedName<IWopiFolder>(rootId, "sub");
        Assert.Equal("sub (1)", name);
    }

    [Fact]
    public async Task GetSuggestedName_FileNoCollision_ReturnsName()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        var name = await _sut.GetSuggestedName<IWopiFile>(rootId, "fresh.txt");
        Assert.Equal("fresh.txt", name);
    }

    [Fact]
    public async Task GetSuggestedName_FileCollision_AppendsCounter()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        // "root.txt" already exists in fixture
        var name = await _sut.GetSuggestedName<IWopiFile>(rootId, "root.txt");
        Assert.Equal("root (1).txt", name);
    }

    [Fact]
    public async Task GetSuggestedName_UnsupportedType_Throws()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _sut.GetSuggestedName<IWopiResource>(rootId, "anything"));
    }

    // ---------- CreateWopiChildResource ----------

    [Fact]
    public async Task CreateWopiChildResource_File_CreatesAndReturnsFile()
    {
        var rootId = _sut.RootContainerPointer.Identifier;

        var file = await _sut.CreateWopiChildResource<IWopiFile>(rootId, "new.txt");

        Assert.NotNull(file);
        Assert.True(File.Exists(Path.Combine(_root.FullName, "new.txt")));
    }

    [Fact]
    public async Task CreateWopiChildResource_FileWithoutContainer_UsesRoot()
    {
        var file = await _sut.CreateWopiChildResource<IWopiFile>(containerId: null, "rootless.txt");

        Assert.NotNull(file);
        Assert.True(File.Exists(Path.Combine(_root.FullName, "rootless.txt")));
    }

    [Fact]
    public async Task CreateWopiChildResource_FileAlreadyExists_Throws()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateWopiChildResource<IWopiFile>(rootId, "root.txt"));
    }

    [Fact]
    public async Task CreateWopiChildResource_FileMissingContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.CreateWopiChildResource<IWopiFile>("missing", "x.txt"));
    }

    [Fact]
    public async Task CreateWopiChildResource_Folder_CreatesAndReturnsFolder()
    {
        var rootId = _sut.RootContainerPointer.Identifier;

        var folder = await _sut.CreateWopiChildResource<IWopiFolder>(rootId, "new-folder");

        Assert.NotNull(folder);
        Assert.True(Directory.Exists(Path.Combine(_root.FullName, "new-folder")));
    }

    [Fact]
    public async Task CreateWopiChildResource_FolderAlreadyExists_Throws()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateWopiChildResource<IWopiFolder>(rootId, "sub"));
    }

    [Fact]
    public async Task CreateWopiChildResource_FolderMissingContainer_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.CreateWopiChildResource<IWopiFolder>("missing", "x"));
    }

    [Fact]
    public async Task CreateWopiChildResource_UnsupportedType_Throws()
    {
        var rootId = _sut.RootContainerPointer.Identifier;
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _sut.CreateWopiChildResource<IWopiResource>(rootId, "x"));
    }

    // ---------- DeleteWopiResource ----------

    [Fact]
    public async Task DeleteWopiResource_ExistingFile_DeletesAndReturnsTrue()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        var ok = await _sut.DeleteWopiResource<IWopiFile>(fileId);

        Assert.True(ok);
        Assert.False(File.Exists(_rootTxtPath));
    }

    [Fact]
    public async Task DeleteWopiResource_FileMissingId_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _sut.DeleteWopiResource<IWopiFile>("missing"));
    }

    [Fact]
    public async Task DeleteWopiResource_FileIdMappedButFileDeleted_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));
        File.Delete(_rootTxtPath);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _sut.DeleteWopiResource<IWopiFile>(fileId));
    }

    [Fact]
    public async Task DeleteWopiResource_EmptyFolder_DeletesAndReturnsTrue()
    {
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));

        var ok = await _sut.DeleteWopiResource<IWopiFolder>(folderId);

        Assert.True(ok);
        Assert.False(Directory.Exists(_empty.FullName));
    }

    [Fact]
    public async Task DeleteWopiResource_NonEmptyFolder_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_sub.FullName, out var folderId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.DeleteWopiResource<IWopiFolder>(folderId));
    }

    [Fact]
    public async Task DeleteWopiResource_FolderMissingId_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.DeleteWopiResource<IWopiFolder>("missing"));
    }

    [Fact]
    public async Task DeleteWopiResource_FolderIdMappedButDirGone_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));
        _empty.Delete(recursive: true);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.DeleteWopiResource<IWopiFolder>(folderId));
    }

    [Fact]
    public async Task DeleteWopiResource_UnsupportedType_Throws()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _sut.DeleteWopiResource<IWopiResource>("anything"));
    }

    // ---------- RenameWopiResource ----------

    [Fact]
    public async Task RenameWopiResource_InvalidName_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.RenameWopiResource<IWopiFile>(fileId, "bad\0name.txt"));
    }

    [Fact]
    public async Task RenameWopiResource_File_RenamesAndReturnsTrue()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        var ok = await _sut.RenameWopiResource<IWopiFile>(fileId, "renamed.txt");

        Assert.True(ok);
        Assert.False(File.Exists(_rootTxtPath));
        Assert.True(File.Exists(Path.Combine(_root.FullName, "renamed.txt")));
    }

    [Fact]
    public async Task RenameWopiResource_FileMissingId_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _sut.RenameWopiResource<IWopiFile>("missing", "x.txt"));
    }

    [Fact]
    public async Task RenameWopiResource_FileTargetExists_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_rootTxtPath, out var fileId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.RenameWopiResource<IWopiFile>(fileId, "root.docx"));
    }

    [Fact]
    public async Task RenameWopiResource_Folder_RenamesAndReturnsTrue()
    {
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));

        var ok = await _sut.RenameWopiResource<IWopiFolder>(folderId, "renamed");

        Assert.True(ok);
        Assert.False(Directory.Exists(_empty.FullName));
        Assert.True(Directory.Exists(Path.Combine(_root.FullName, "renamed")));
    }

    [Fact]
    public async Task RenameWopiResource_FolderMissingId_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _sut.RenameWopiResource<IWopiFolder>("missing", "x"));
    }

    [Fact]
    public async Task RenameWopiResource_FolderTargetExists_Throws()
    {
        Assert.True(_fileIds.TryGetFileId(_empty.FullName, out var folderId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.RenameWopiResource<IWopiFolder>(folderId, "sub"));
    }

    [Fact]
    public async Task RenameWopiResource_UnsupportedType_Throws()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _sut.RenameWopiResource<IWopiResource>("anything", "x"));
    }
}
