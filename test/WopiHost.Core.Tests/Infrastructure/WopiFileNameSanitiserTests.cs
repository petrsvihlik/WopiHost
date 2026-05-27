using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="WopiFileNameSanitiser"/> — the shared scrub helper that
/// <see cref="WopiHost.Core.Endpoints.FileMutatingEndpoints"/>,
/// <see cref="WopiHost.Core.Endpoints.ContainerMutatingEndpoints"/>, and
/// <see cref="DefaultWopiNewChildFileNegotiator"/> all delegate to. The integration tests cover
/// the round-trip through the storage provider; these tests pin the pure-string transformations
/// so a future scrub-rule tweak surfaces here instead of at the provider boundary.
/// </summary>
public class WopiFileNameSanitiserTests
{
    [Theory]
    [InlineData("clean", "clean")]
    [InlineData("with space", "with_space")]              // space → `_` (in ForbiddenChars)
    [InlineData("  padded  ", "__padded__")]              // spaces are replaced BEFORE Trim — Trim catches non-space whitespace only
    [InlineData("\tpadded\t", "padded")]                  // tab is whitespace but NOT forbidden → Trim strips it
    [InlineData("bad<>chars", "bad__chars")]
    [InlineData("a/b\\c:d", "a_b_c_d")]
    [InlineData("\"quoted\"", "_quoted_")]
    [InlineData("a|b?c*d", "a_b_c_d")]
    public void ScrubOrNull_ReplacesForbiddenChars_AndTrimsNonForbiddenWhitespace(string input, string expected)
    {
        Assert.Equal(expected, WopiFileNameSanitiser.ScrubOrNull(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]      // tab not in ForbiddenChars but stripped by Trim → empty
    [InlineData("\t\t\n")]
    [InlineData(".")]
    [InlineData("..")]
    public void ScrubOrNull_ReturnsNull_ForEmptyOrPathNav(string input)
    {
        Assert.Null(WopiFileNameSanitiser.ScrubOrNull(input));
    }

    [Theory]
    [InlineData(" ")]        // single space → "_" (non-null — would have been null pre-scrub)
    [InlineData("   ")]      // all spaces → "___" (non-null after the Replace)
    public void ScrubOrNull_AllSpaces_ReturnsUnderscores_NotNull(string input)
    {
        // Pins the subtle ordering: Replace(' ', '_') runs before Trim(), so an all-spaces input
        // becomes a non-empty run of underscores rather than null. If we wanted "all spaces → null"
        // we'd need to either drop ' ' from ForbiddenChars and rely on Trim, or check empty BEFORE
        // the Replace. The current behaviour is intentional — a stem of underscores is a usable
        // (if ugly) filename; the caller's fallback path handles it via CheckValidFileName.
        Assert.Equal(new string('_', input.Length), WopiFileNameSanitiser.ScrubOrNull(input));
    }

    [Theory]
    [InlineData("file.docx", ".docx")]
    [InlineData("archive.tar.gz", ".gz")]   // last-dot wins
    [InlineData("noext", "")]
    [InlineData(".hidden", "")]             // leading-dot-only → no extension per the helper contract
    [InlineData("trailing.", ".")]
    public void ExtractExtension_FindsLastDot_OrEmpty(string input, string expected)
    {
        Assert.Equal(expected, WopiFileNameSanitiser.ExtractExtension(input));
    }

    [Fact]
    public async Task TryBuildValidCandidateAsync_ScrubsAndPassesProviderCheck_ReturnsCandidate()
    {
        var writable = new Mock<IWopiWritableStorageProvider>();
        writable.Setup(w => w.CheckValidFileName("bad_name.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await WopiFileNameSanitiser.TryBuildValidCandidateAsync(
            writable.Object, "bad/name.docx", fallbackStem: "fallback", CancellationToken.None);

        Assert.Equal("bad_name.docx", result);
    }

    [Fact]
    public async Task TryBuildValidCandidateAsync_ProviderRejectsScrubbed_ReturnsNull()
    {
        // Provider says no even after scrubbing — caller (RenameFile / negotiator) decides
        // whether to swap in its own fallback shape.
        var writable = new Mock<IWopiWritableStorageProvider>();
        writable.Setup(w => w.CheckValidFileName(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await WopiFileNameSanitiser.TryBuildValidCandidateAsync(
            writable.Object, "bad/name.docx", fallbackStem: "fallback", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryBuildValidCandidateAsync_EmptyOrPathNavStem_SubstitutesFallback()
    {
        // Input ".docx" → ExtractExtension yields ".docx" (no, wait — leading-dot-only means
        // ext = "" by the helper contract). So stem = ".docx", scrubbed = ".docx" (no forbidden
        // chars), not in {".", ".."} → returns ".docx" as candidate. To actually exercise the
        // fallback substitution we need a stem that scrubs to null. Use ".." which IS path-nav.
        var writable = new Mock<IWopiWritableStorageProvider>();
        writable.Setup(w => w.CheckValidFileName("fallback.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await WopiFileNameSanitiser.TryBuildValidCandidateAsync(
            writable.Object, "...docx", fallbackStem: "fallback", CancellationToken.None);
        // "...docx" → last dot at index 3 → ext = ".docx", stem = ".." → ScrubOrNull(".." ) == null
        // → fallback kicks in → "fallback" + ".docx" = "fallback.docx".

        Assert.Equal("fallback.docx", result);
    }

    [Fact]
    public async Task TryBuildValidCandidateAsync_PreservesExtension_ThroughScrub()
    {
        // The scrub touches only the stem; the extension (last `.` onwards) is preserved
        // verbatim even if the original stem contained forbidden chars. This pins the spec
        // requirement: "modify the proposed name as needed ... while preserving the file extension."
        var writable = new Mock<IWopiWritableStorageProvider>();
        writable.Setup(w => w.CheckValidFileName("a_b_c.docx", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await WopiFileNameSanitiser.TryBuildValidCandidateAsync(
            writable.Object, "a/b/c.docx", fallbackStem: "fallback", CancellationToken.None);

        Assert.Equal("a_b_c.docx", result);
    }

    [Fact]
    public async Task TryBuildValidCandidateAsync_NoExtension_AppendsFallbackUnchanged()
    {
        // Input with no `.` — ExtractExtension returns "" so the candidate is just the
        // scrubbed stem (no extension appended).
        var writable = new Mock<IWopiWritableStorageProvider>();
        writable.Setup(w => w.CheckValidFileName("scrubbed_name", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await WopiFileNameSanitiser.TryBuildValidCandidateAsync(
            writable.Object, "scrubbed name", fallbackStem: "fallback", CancellationToken.None);

        Assert.Equal("scrubbed_name", result);
    }
}
