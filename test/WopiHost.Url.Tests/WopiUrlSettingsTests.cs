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
}
