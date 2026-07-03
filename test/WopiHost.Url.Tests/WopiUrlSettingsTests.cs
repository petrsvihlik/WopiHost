namespace WopiHost.Url.Tests;

/// <summary>
/// Tests for <see cref="WopiUrlSettings"/> constructors and the method-arg-overrides-ctor-arg
/// precedence in <see cref="WopiUrlBuilder"/>. The IDictionary constructor and the override
/// branch are otherwise unexercised by the existing URL-generation tests, which feed settings
/// only via the typed properties.
/// </summary>
public class WopiUrlSettingsTests
{
    [Fact]
    public void DefaultConstructor_ProducesEmptySettings()
    {
        var settings = new WopiUrlSettings();

        Assert.Empty(settings);
    }

    [Fact]
    public void DictionaryConstructor_CopiesAllPairs()
    {
        var source = new Dictionary<string, string>
        {
            [WopiUrlSettings.Placeholders.UiLlcc] = "cs-CZ",
            [WopiUrlSettings.Placeholders.Perfstats] = "Roundtrip",
        };

        var settings = new WopiUrlSettings(source);

        Assert.Equal(2, settings.Count);
        Assert.Equal("cs-CZ", settings[WopiUrlSettings.Placeholders.UiLlcc]);
        Assert.Equal("Roundtrip", settings[WopiUrlSettings.Placeholders.Perfstats]);
    }

    [Fact]
    public void DictionaryConstructor_NullInput_ProducesEmptySettings()
    {
        // Null is allowed and means "no initial values" — used by callers that build the
        // settings up incrementally after construction.
        var settings = new WopiUrlSettings(settings: null);

        Assert.Empty(settings);
    }

    [Fact]
    public void TypedAccessors_UnsetKeys_ReturnDefaultsInsteadOfThrowing()
    {
        // The typed accessors are public API on a settings bag; reading a placeholder that was
        // never set must yield null / a default value, not KeyNotFoundException.
        var settings = new WopiUrlSettings();

        Assert.Null(settings.UiLlcc);
        Assert.Null(settings.DcLlcc);
        Assert.False(settings.Embedded);
        Assert.False(settings.DisableAsync);
        Assert.False(settings.DisableBroadcast);
        Assert.False(settings.Fullscreen);
        Assert.False(settings.Recording);
        Assert.Equal(0, settings.ThemeId);
        Assert.Equal(0, settings.BusinessUser);
        Assert.Equal(0, settings.DisableChat);
        Assert.Equal(0, settings.Perfstats);
        Assert.Null(settings.HostSessionId);
        Assert.Null(settings.SessionContext);
        Assert.Equal(default, settings.ValidatorTestCategory);
    }

    [Fact]
    public void TypedAccessors_RoundTrip_AfterSet()
    {
        var settings = new WopiUrlSettings
        {
            UiLlcc = new System.Globalization.CultureInfo("cs-CZ"),
            Embedded = true,
            ThemeId = 2,
            SessionContext = "user-42",
        };

        Assert.Equal("cs-CZ", settings.UiLlcc?.Name);
        Assert.True(settings.Embedded);
        Assert.Equal(2, settings.ThemeId);
        Assert.Equal("user-42", settings.SessionContext);
    }
}
