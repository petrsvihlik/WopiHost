using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;
using WopiHost.Core.Endpoints;

namespace WopiHost.Core.Tests.Endpoints;

/// <summary>
/// Unit tests for <see cref="EndpointHelpers.TryParseWopiSrc"/>. Relocated from the deleted
/// WopiBootstrapperControllerTests.TryParseWopiSrc_* tests as part of the #430 phase 4 / 5
/// migration — the parser implementation moved from the controller to EndpointHelpers in
/// phase 3 and the tests follow it here. IssueEcosystemPointerAsync is covered by the
/// EndpointSmokeTests integration suite (FileEcosystemPointer / ContainerEcosystemPointer).
/// </summary>
public class EndpointHelpersTests
{
    [Theory]
    [InlineData("https://wopi.example.com/wopi/files/abc", WopiResourceType.File, "abc")]
    [InlineData("https://wopi.example.com/wopi/containers/abc", WopiResourceType.Container, "abc")]
    [InlineData("https://wopi.example.com/wopi/files/abc?access_token=t", WopiResourceType.File, "abc")]
    [InlineData("https://wopi.example.com/wopi/files/abc#frag", WopiResourceType.File, "abc")]
    [InlineData("https://wopi.example.com/wopi/files/some%20file", WopiResourceType.File, "some file")]
    [InlineData("https://wopi.example.com/wopi/files/abc/", WopiResourceType.File, "abc")] // tolerates trailing slash
    [InlineData("https://wopi.example.com/some/wopi/Files/CASE_INSENSITIVE", WopiResourceType.File, "CASE_INSENSITIVE")]
    [InlineData("https://wopi.example.com/wopi/CONTAINERS/abc", WopiResourceType.Container, "abc")]
    // Trailing-anchor wins: the path contains both an earlier "files" segment and a later
    // "containers" segment. The current regex-based parser correctly resolves to the resource
    // at the URL tail (containers/abc); the previous segment-scan implementation would have
    // mis-parsed this as files/archive.
    [InlineData("https://wopi.example.com/files/archive/containers/abc", WopiResourceType.Container, "abc")]
    [InlineData("https://wopi.example.com/containers/parent/files/child", WopiResourceType.File, "child")]
    public void TryParseWopiSrc_ValidUrls(string url, WopiResourceType expectedType, string expectedId)
    {
        var ok = EndpointHelpers.TryParseWopiSrc(url, out var type, out var id);

        Assert.True(ok);
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("https://wopi.example.com/elsewhere/abc")]
    [InlineData("https://wopi.example.com/wopi/files/")] // missing id
    [InlineData("https://wopi.example.com/wopi/files")] // missing id segment entirely
    [InlineData("https://wopi.example.com/wopi/containers/")] // missing id
    public void TryParseWopiSrc_RejectsInvalidUrls(string url)
    {
        var ok = EndpointHelpers.TryParseWopiSrc(url, out _, out _);

        Assert.False(ok);
    }

    // EnsureExactlyOneOf — replaces the hand-rolled mutex check used in name-negotiation
    // endpoints (PutRelativeFile, CreateChildFile, CreateChildContainer). The spec mandates
    // 501 — not 400 — when both targets are present OR both are absent; whitespace-only values
    // count as absent so a header sent with an empty token doesn't sneak past the gate.
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("a", "b")]
    [InlineData(null, "")]
    [InlineData("   ", null)]
    public void EnsureExactlyOneOf_BothPresentOrBothAbsent_Returns501(string? a, string? b)
    {
        var result = EndpointHelpers.EnsureExactlyOneOf(a, b);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status501NotImplemented, result!.StatusCode);
    }

    [Theory]
    [InlineData("a", null)]
    [InlineData(null, "b")]
    [InlineData("a", "")]
    [InlineData("", "b")]
    [InlineData("a", "   ")] // whitespace-only treated as absent — exactly-one rule still satisfied
    [InlineData("   ", "b")]
    public void EnsureExactlyOneOf_ExactlyOneSet_ReturnsNull(string? a, string? b)
    {
        var result = EndpointHelpers.EnsureExactlyOneOf(a, b);

        Assert.Null(result);
    }
}
