using System.Globalization;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Url.Tests;

public class WopiUrlGeneratorTests
{
    private readonly IDiscoverer _discoverer;

    public WopiUrlGeneratorTests()
    {
        _discoverer = A.Fake<IDiscoverer>();
        A.CallTo(() => _discoverer.GetUrlTemplateAsync("xlsx", WopiActionEnum.Edit)).ReturnsLazily(() => "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>");
        A.CallTo(() => _discoverer.GetUrlTemplateAsync("docx", WopiActionEnum.View)).ReturnsLazily(() => "http://owaserver/wv/wordviewerframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>");
    }

    [Theory]
    [InlineData("xlsx", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.xlsx")]
    [InlineData("docx", "http://wopihost:5000/wopi/files/test.docx", WopiActionEnum.View, "http://owaserver/wv/wordviewerframe.aspx?&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.docx")]
    public async Task GetFileUrlAsync_WithoutAdditionalSettings_ReturnsExpectedUrl(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
    {
        var urlGenerator = new WopiUrlBuilder(_discoverer, NullLogger<WopiUrlBuilder>.Instance);

        var result = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiFileUrl), action);

        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData("xlsx", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&ui=en-US&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.xlsx")]
    [InlineData("docx", "http://wopihost:5000/wopi/files/test.docx", WopiActionEnum.View, "http://owaserver/wv/wordviewerframe.aspx?ui=en-US&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.docx")]
    public async Task GetFileUrlAsync_WithAdditionalSettings_ReturnsExpectedUrl(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
    {
        var settings = new WopiUrlSettings { UiLlcc = new CultureInfo("en-US") };
        var urlGenerator = new WopiUrlBuilder(_discoverer, NullLogger<WopiUrlBuilder>.Instance, settings);

        var result = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiFileUrl), action);

        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData("html", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, null)]
    public async Task GetFileUrlAsync_UnknownTemplate_ReturnsNull(string extension, string wopiFileUrl, WopiActionEnum action, string? expectedValue)
    {
        var urlGenerator = new WopiUrlBuilder(_discoverer, NullLogger<WopiUrlBuilder>.Instance);

        var result = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiFileUrl), action);

        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public async Task GetFileUrlAsync_TemplateContainsWopiSourcePlaceholder_SubstitutesAndDoesNotAppendDuplicate()
    {
        // Modern OOS / M365 templates carry `<wopisrc=WOPI_SOURCE&>`. Prior to the WOPI_SOURCE fix
        // the builder would unconditionally append `&WOPISrc=...`, producing two WopiSrc params
        // (lowercase from the substitution + uppercase from the append). The placeholder must now
        // be auto-populated from the wopiFileUrl parameter and the unconditional append skipped.
        const string template = "https://office.example.com/x/_vti_bin/excelserviceinternal.asmx?<ui=UI_LLCC&><wopisrc=WOPI_SOURCE&>";
        A.CallTo(() => _discoverer.GetUrlTemplateAsync("xlsm", WopiActionEnum.LegacyWebService)).ReturnsLazily(() => template);

        var urlGenerator = new WopiUrlBuilder(_discoverer, NullLogger<WopiUrlBuilder>.Instance);
        var fileUrl = new Uri("http://wopihost:5000/wopi/files/test.xlsm");

        var result = await urlGenerator.GetFileUrlAsync("xlsm", fileUrl, WopiActionEnum.LegacyWebService);

        Assert.NotNull(result);
        // The substitution is URL-escaped and lowercase per the template.
        Assert.Contains("wopisrc=" + Uri.EscapeDataString(fileUrl.ToString()), result, StringComparison.Ordinal);
        // No second copy of the param — case-insensitive count of "wopisrc=" must be exactly 1.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(result, "wopisrc=", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [Fact]
    public async Task GetFileUrlAsync_TemplateWithoutWopiSourcePlaceholder_AppendsWopiSrcExactlyOnce()
    {
        // Backward-compat path: templates that don't include the WOPI_SOURCE placeholder still
        // get the unconditional `&WOPISrc=` append.
        var urlGenerator = new WopiUrlBuilder(_discoverer, NullLogger<WopiUrlBuilder>.Instance);
        var fileUrl = new Uri("http://wopihost:5000/wopi/files/test.xlsx");

        var result = await urlGenerator.GetFileUrlAsync("xlsx", fileUrl, WopiActionEnum.Edit);

        Assert.NotNull(result);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(result, "wopisrc=", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Assert.Contains("WOPISrc=" + Uri.EscapeDataString(fileUrl.ToString()), result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileUrlAsync_CallerProvidedWopiSourceSetting_IsOverriddenByFileUrlParameter()
    {
        // Defensive: even if a caller leaks WOPI_SOURCE into urlSettings, the wopiFileUrl
        // parameter wins. The builder owns the single source of truth for the WopiSrc value.
        const string template = "https://office.example.com/x/_layouts/foo.aspx?<wopisrc=WOPI_SOURCE&>";
        A.CallTo(() => _discoverer.GetUrlTemplateAsync("xlsm", WopiActionEnum.LegacyWebService)).ReturnsLazily(() => template);

        var settings = new WopiUrlSettings { ["WOPI_SOURCE"] = "http://impostor/file/999" };
        var urlGenerator = new WopiUrlBuilder(_discoverer, NullLogger<WopiUrlBuilder>.Instance, settings);
        var fileUrl = new Uri("http://wopihost:5000/wopi/files/real.xlsm");

        var result = await urlGenerator.GetFileUrlAsync("xlsm", fileUrl, WopiActionEnum.LegacyWebService);

        Assert.NotNull(result);
        Assert.Contains("wopisrc=" + Uri.EscapeDataString(fileUrl.ToString()), result, StringComparison.Ordinal);
        Assert.DoesNotContain("impostor", result, StringComparison.Ordinal);
    }

    [Fact]
    public void WopiUrlSettings_AssignedProperties_ExposeSameValues()
    {
        var businessUser = new Random(DateTime.Now.Millisecond).Next();
        var uiLlcc = new CultureInfo("en-US");
        var dcLlcc = new CultureInfo("es-ES");
        var embedded = true;
        var disableAsync = true;
        var disableBroadcast = true;
        var fullscreen = true;
        var recording = true;
        var themeId = new Random(DateTime.Now.Millisecond).Next();
        var disableChat = new Random(DateTime.Now.Millisecond).Next();
        var perfstats = new Random(DateTime.Now.Millisecond).Next();
        var hostSessionId = Guid.NewGuid().ToString();
        var sessionContext = Guid.NewGuid().ToString();
        var validatorTestCategory = ValidatorTestCategoryEnum.All;

        var settings = new WopiUrlSettings()
        {
            BusinessUser = businessUser,
            UiLlcc = uiLlcc,
            DcLlcc = dcLlcc,
            Embedded = embedded,
            DisableAsync = disableAsync,
            DisableBroadcast = disableBroadcast,
            Fullscreen = fullscreen,
            Recording = recording,
            ThemeId = themeId,
            DisableChat = disableChat,
            Perfstats = perfstats,
            HostSessionId = hostSessionId,
            SessionContext = sessionContext,
            ValidatorTestCategory = validatorTestCategory
        };

        Assert.Equal(14, settings.Count);
        Assert.Equal(businessUser, settings.BusinessUser);
        Assert.Equal(uiLlcc, settings.UiLlcc);
        Assert.Equal(dcLlcc, settings.DcLlcc);
        Assert.Equal(embedded, settings.Embedded);
        Assert.Equal(disableAsync, settings.DisableAsync);
        Assert.Equal(disableBroadcast, settings.DisableBroadcast);
        Assert.Equal(fullscreen, settings.Fullscreen);
        Assert.Equal(recording, settings.Recording);
        Assert.Equal(themeId, settings.ThemeId);
        Assert.Equal(disableChat, settings.DisableChat);
        Assert.Equal(perfstats, settings.Perfstats);
        Assert.Equal(hostSessionId, settings.HostSessionId);
        Assert.Equal(sessionContext, settings.SessionContext);
        Assert.Equal(validatorTestCategory, settings.ValidatorTestCategory);
    }
}
