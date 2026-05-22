using System.Security.Claims;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Tests for the WOPI spec's PutRelativeFile / CreateChildFile name-negotiation protocol as
/// implemented by <see cref="DefaultWopiNewChildFileNegotiator"/>. Each test pins one branch of
/// the spec dance — relative vs. suggested mode, collision-vs-not, overwrite-vs-not,
/// locked-vs-not — and asserts the outcome a controller will translate into the wire response.
/// </summary>
public class DefaultWopiNewChildFileNegotiatorTests
{
    private const string Container = "container-1";
    private static readonly ClaimsPrincipal s_anonymous = new();
    private readonly Mock<IWopiStorageProvider> _storage = new();
    private readonly Mock<IWopiWritableStorageProvider> _writable = new();
    private readonly Mock<IWopiPermissionProvider> _permissions = new();
    private readonly Mock<IWopiLockProvider> _lockProvider = new();

    public DefaultWopiNewChildFileNegotiatorTests()
    {
        // Default permission stance for every test: allow overwrite. The 501-unauthorized-
        // overwrite path has its own dedicated test that overrides this setup.
        _permissions
            .Setup(p => p.CanOverwriteFileAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private DefaultWopiNewChildFileNegotiator CreateNegotiator(bool withLockProvider = true)
        => new(_storage.Object, _writable.Object, _permissions.Object, withLockProvider ? _lockProvider.Object : null);

    private static WopiNewChildFileRequest Request(
        string? suggested = null,
        string? relative = null,
        bool overwrite = false,
        string fallbackStem = "Untitled",
        ClaimsPrincipal? user = null)
        => new(Container, suggested, relative, overwrite, fallbackStem, user ?? s_anonymous);

    [Fact]
    public async Task BothTargetsMissing_ReturnsBadRequest()
    {
        // Controllers short-circuit this case with 501 before invoking the negotiator; the
        // negotiator itself defensively maps to BadRequest for direct callers that skip the
        // pre-check. Covers the trailing-return branch of NegotiateAsync.
        var result = await CreateNegotiator().NegotiateAsync(Request());

        Assert.Equal(WopiNewChildFileOutcome.BadRequest, result.Outcome);
    }

    [Fact]
    public async Task RelativeTarget_InvalidName_ReturnsBadRequest_WithSuggestion()
    {
        // Initial name fails validation; the sanitised candidate ("bad_name.docx" — '/' → '_')
        // is then re-validated and dedup-suggested. Spec: the host MAY include
        // X-WOPI-ValidRelativeTarget on the 400 so the client can auto-retry.
        _writable.Setup(w => w.CheckValidFileName("bad/name.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _writable.Setup(w => w.CheckValidFileName("bad_name.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writable.Setup(w => w.GetSuggestedFileName(Container, "bad_name.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bad_name.docx");

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "bad/name.docx"));

        Assert.Equal(WopiNewChildFileOutcome.BadRequest, result.Outcome);
        Assert.Equal("bad_name.docx", result.ValidRelativeTargetSuggestion);
    }

    [Fact]
    public async Task RelativeTarget_InvalidName_OmitsSuggestionWhenSanitiseStillFails()
    {
        // Sanitised candidate also fails (e.g. provider rejects reserved names like CON);
        // omit X-WOPI-ValidRelativeTarget rather than emit a name we know is invalid.
        _writable.Setup(w => w.CheckValidFileName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "CON.docx"));

        Assert.Equal(WopiNewChildFileOutcome.BadRequest, result.Outcome);
        Assert.Null(result.ValidRelativeTargetSuggestion);
    }

    [Fact]
    public async Task RelativeTarget_NoCollision_CreatesFile()
    {
        var newFile = Mock.Of<IWopiWritableFile>();
        _writable.Setup(w => w.CheckValidFileName("new.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storage.Setup(s => s.GetWopiFileByName(Container, "new.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiFile?)null);
        _writable.Setup(w => w.CreateWopiChildFile(Container, "new.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFile);

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "new.docx"));

        Assert.Equal(WopiNewChildFileOutcome.Success, result.Outcome);
        Assert.Same(newFile, result.File);
    }

    [Fact]
    public async Task RelativeTarget_NoCollision_CreateReturnsNull_ReturnsInternalError()
    {
        _writable.Setup(w => w.CheckValidFileName("new.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storage.Setup(s => s.GetWopiFileByName(Container, "new.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiFile?)null);
        _writable.Setup(w => w.CreateWopiChildFile(Container, "new.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiWritableFile?)null);

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "new.docx"));

        Assert.Equal(WopiNewChildFileOutcome.InternalError, result.Outcome);
    }

    [Fact]
    public async Task RelativeTarget_Collision_NoOverwrite_ReturnsConflictWithSuggestion()
    {
        var existing = Mock.Of<IWopiFile>(f => f.Identifier == "existing-id");
        _writable.Setup(w => w.CheckValidFileName("doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storage.Setup(s => s.GetWopiFileByName(Container, "doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _writable.Setup(w => w.GetSuggestedFileName(Container, "doc.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc (1).docx");

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "doc.docx", overwrite: false));

        Assert.Equal(WopiNewChildFileOutcome.Conflict, result.Outcome);
        Assert.Equal("doc (1).docx", result.ValidRelativeTargetSuggestion);
    }

    [Fact]
    public async Task RelativeTarget_Collision_Overwrite_PermissionProviderDenies_ReturnsNotImplemented()
    {
        // Spec (#455): "If the user is not authorized to overwrite the target file, the host must
        // respond with a 501 Not Implemented." The endpoint-level Permission.Create gate already
        // passed (we're inside the negotiator), but the per-target permission provider rules out
        // overwriting THIS specific file. The lock probe must NOT run — denying overwrite is a
        // strictly stronger signal than "the existing file is locked," so 501 wins.
        var existing = Mock.Of<IWopiFile>(f => f.Identifier == "existing-id");
        var deniedUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "alice")], "test"));
        _writable.Setup(w => w.CheckValidFileName("doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storage.Setup(s => s.GetWopiFileByName(Container, "doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _permissions
            .Setup(p => p.CanOverwriteFileAsync(deniedUser, existing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "doc.docx", overwrite: true, user: deniedUser));

        Assert.Equal(WopiNewChildFileOutcome.NotImplemented, result.Outcome);
        _lockProvider.Verify(
            l => l.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Permission denial must short-circuit before the lock probe.");
        _writable.Verify(
            w => w.GetWritableFile(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Permission denial must short-circuit before the writable-file resolution.");
    }

    [Fact]
    public async Task RelativeTarget_Collision_Overwrite_Locked_ReturnsLocked()
    {
        var existing = Mock.Of<IWopiFile>(f => f.Identifier == "existing-id");
        _writable.Setup(w => w.CheckValidFileName("doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storage.Setup(s => s.GetWopiFileByName(Container, "doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _lockProvider.Setup(l => l.GetLockAsync("existing-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { FileId = "existing-id", LockId = "L-1" });

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "doc.docx", overwrite: true));

        Assert.Equal(WopiNewChildFileOutcome.Locked, result.Outcome);
        Assert.Equal("L-1", result.ExistingLockId);
    }

    [Fact]
    public async Task RelativeTarget_Collision_Overwrite_Unlocked_ReturnsSuccessFromGetWritableFile()
    {
        var existing = Mock.Of<IWopiFile>(f => f.Identifier == "existing-id");
        var writableExisting = Mock.Of<IWopiWritableFile>();
        _writable.Setup(w => w.CheckValidFileName("doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storage.Setup(s => s.GetWopiFileByName(Container, "doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _lockProvider.Setup(l => l.GetLockAsync("existing-id", It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writable.Setup(w => w.GetWritableFile("existing-id", It.IsAny<CancellationToken>())).ReturnsAsync(writableExisting);

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "doc.docx", overwrite: true));

        Assert.Equal(WopiNewChildFileOutcome.Success, result.Outcome);
        Assert.Same(writableExisting, result.File);
    }

    [Fact]
    public async Task RelativeTarget_Collision_Overwrite_Unlocked_GetWritableFileReturnsNull_ReturnsInternalError()
    {
        // The writable provider's contract says GetWritableFile returns non-null for a file the
        // read-side already resolved by name. A null here is defensive — covers line 86.
        var existing = Mock.Of<IWopiFile>(f => f.Identifier == "existing-id");
        _writable.Setup(w => w.CheckValidFileName("doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storage.Setup(s => s.GetWopiFileByName(Container, "doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _lockProvider.Setup(l => l.GetLockAsync("existing-id", It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writable.Setup(w => w.GetWritableFile("existing-id", It.IsAny<CancellationToken>())).ReturnsAsync((IWopiWritableFile?)null);

        var result = await CreateNegotiator().NegotiateAsync(Request(relative: "doc.docx", overwrite: true));

        Assert.Equal(WopiNewChildFileOutcome.InternalError, result.Outcome);
    }

    [Fact]
    public async Task RelativeTarget_Collision_Overwrite_NoLockProvider_SkipsLockProbe()
    {
        // Hosts without a lock provider registered still need to honor overwrite — the lock probe
        // is conditional on lockProvider being non-null. Covers the "lockProvider is null" branch
        // (we exercise both halves: this test for the null case, the previous two for non-null).
        var existing = Mock.Of<IWopiFile>(f => f.Identifier == "existing-id");
        var writableExisting = Mock.Of<IWopiWritableFile>();
        _writable.Setup(w => w.CheckValidFileName("doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storage.Setup(s => s.GetWopiFileByName(Container, "doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _writable.Setup(w => w.GetWritableFile("existing-id", It.IsAny<CancellationToken>())).ReturnsAsync(writableExisting);

        var result = await CreateNegotiator(withLockProvider: false)
            .NegotiateAsync(Request(relative: "doc.docx", overwrite: true));

        Assert.Equal(WopiNewChildFileOutcome.Success, result.Outcome);
        _lockProvider.Verify(l => l.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestedTarget_ExtensionOnly_CombinesWithFallbackStem()
    {
        // Extension-only suggested target: ".pdf" combines with the caller-supplied stem to
        // produce the actual name. PutRelativeFile passes the original file's stem;
        // CreateChildFile passes a fresh GUID — the negotiator doesn't care which.
        var newFile = Mock.Of<IWopiWritableFile>();
        _writable.Setup(w => w.GetSuggestedFileName(Container, "Untitled.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Untitled.pdf");
        _writable.Setup(w => w.CreateWopiChildFile(Container, "Untitled.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFile);

        var result = await CreateNegotiator().NegotiateAsync(Request(suggested: ".pdf"));

        Assert.Equal(WopiNewChildFileOutcome.Success, result.Outcome);
        // Validate that the .pdf name was prepended with the fallback stem before deduplication.
        _writable.Verify(w => w.GetSuggestedFileName(Container, "Untitled.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuggestedTarget_InvalidName_SanitisesAndCreates()
    {
        // Spec: PutRelativeFile in suggested mode MUST NOT return 400 / 409 for an invalid name —
        // the host must modify the proposed name to be valid while preserving the file extension.
        // The negotiator sanitises forbidden chars ('/' → '_'), so "bad/name.docx" becomes
        // "bad_name.docx" which then passes validation and gets created.
        var newFile = Mock.Of<IWopiWritableFile>();
        _writable.Setup(w => w.CheckValidFileName("bad/name.docx", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _writable.Setup(w => w.CheckValidFileName("bad_name.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writable.Setup(w => w.GetSuggestedFileName(Container, "bad_name.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bad_name.docx");
        _writable.Setup(w => w.CreateWopiChildFile(Container, "bad_name.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFile);

        var result = await CreateNegotiator().NegotiateAsync(Request(suggested: "bad/name.docx"));

        Assert.Equal(WopiNewChildFileOutcome.Success, result.Outcome);
        Assert.Same(newFile, result.File);
    }

    [Fact]
    public async Task SuggestedTarget_InvalidName_FallsBackToStemWhenSanitiseStillFails()
    {
        // Sanitised candidate also fails validation → fall back to fallbackStem + extension
        // (same shape as the extension-only suggested-target path). Original extension is
        // preserved per spec.
        var newFile = Mock.Of<IWopiWritableFile>();
        _writable.Setup(w => w.CheckValidFileName("bad/name.docx", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _writable.Setup(w => w.CheckValidFileName("bad_name.docx", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _writable.Setup(w => w.GetSuggestedFileName(Container, "Untitled.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Untitled.docx");
        _writable.Setup(w => w.CreateWopiChildFile(Container, "Untitled.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFile);

        var result = await CreateNegotiator().NegotiateAsync(Request(suggested: "bad/name.docx"));

        Assert.Equal(WopiNewChildFileOutcome.Success, result.Outcome);
        _writable.Verify(w => w.GetSuggestedFileName(Container, "Untitled.docx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuggestedTarget_HappyPath_DeduplicatesAndCreates()
    {
        var newFile = Mock.Of<IWopiWritableFile>();
        _writable.Setup(w => w.CheckValidFileName("doc.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writable.Setup(w => w.GetSuggestedFileName(Container, "doc.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc (1).docx");
        _writable.Setup(w => w.CreateWopiChildFile(Container, "doc (1).docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFile);

        var result = await CreateNegotiator().NegotiateAsync(Request(suggested: "doc.docx"));

        Assert.Equal(WopiNewChildFileOutcome.Success, result.Outcome);
        Assert.Same(newFile, result.File);
    }

    [Fact]
    public async Task SuggestedTarget_CreateReturnsNull_ReturnsInternalError()
    {
        _writable.Setup(w => w.CheckValidFileName(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writable.Setup(w => w.GetSuggestedFileName(Container, "doc.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc.docx");
        _writable.Setup(w => w.CreateWopiChildFile(Container, "doc.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiWritableFile?)null);

        var result = await CreateNegotiator().NegotiateAsync(Request(suggested: "doc.docx"));

        Assert.Equal(WopiNewChildFileOutcome.InternalError, result.Outcome);
    }

    [Fact]
    public async Task NullRequest_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => CreateNegotiator().NegotiateAsync(null!));
}
