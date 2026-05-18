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
    [InlineData("https://wopi.example.com/wopi/files/some%20file", WopiResourceType.File, "some file")]
    [InlineData("https://wopi.example.com/some/wopi/Files/CASE_INSENSITIVE", WopiResourceType.File, "CASE_INSENSITIVE")]
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
    public void TryParseWopiSrc_RejectsInvalidUrls(string url)
    {
        var ok = EndpointHelpers.TryParseWopiSrc(url, out _, out _);

        Assert.False(ok);
    }
}
