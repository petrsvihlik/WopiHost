using System.Globalization;
using FakeItEasy;
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
    public async void UrlWithoutAdditionalSettings(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
    {
        // Arrange
        var urlGenerator = new WopiUrlBuilder(_discoverer);

        // Act
        var result = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiFileUrl), action);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData("xlsx", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&ui=en-US&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.xlsx")]
    [InlineData("docx", "http://wopihost:5000/wopi/files/test.docx", WopiActionEnum.View, "http://owaserver/wv/wordviewerframe.aspx?ui=en-US&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.docx")]
    public async void UrlWithAdditionalSettings(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
    {
        // Arrange
        var settings = new WopiUrlSettings { UiLlcc = new CultureInfo("en-US") };
        var urlGenerator = new WopiUrlBuilder(_discoverer, settings);

        // Act
        var result = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiFileUrl), action);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData("html", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, null)]
    public async void NonExistentTemplate(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
    {
        // Arrange
        var urlGenerator = new WopiUrlBuilder(_discoverer);

        // Act
        var result = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiFileUrl), action);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void SettingsArePresent()
    {
        // Arrange
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
        var wopiSource = "c:\\doc.docx";
        var validatorTestCategory = ValidatorTestCategoryEnum.All;

        // Act
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
            WopiSource = wopiSource,
            ValidatorTestCategory = validatorTestCategory
        };

        // Assert
        Assert.Equal(15, settings.Count);
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
        Assert.Equal(wopiSource, settings.WopiSource);
        Assert.Equal(validatorTestCategory, settings.ValidatorTestCategory);
    }
}
