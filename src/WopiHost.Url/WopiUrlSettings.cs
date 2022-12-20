using System.Globalization;

namespace WopiHost.Url;

/// <summary>
/// Additional URL settings influencing the behavior of the WOPI client (as defined in https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/online/discovery#placeholder-values).
/// Settings are used to replace the placeholders in URL templates retrieved from the WOPI discovery file.
/// </summary>
public class WopiUrlSettings : Dictionary<string, string>
{
    /// <summary>
    /// Indicates that the WOPI server MAY include the preferred UI language in the format described in [RFC1766].
    /// </summary>
    public CultureInfo UiLlcc
    {
        get => new(this["UI_LLCC"]);
        set
        {
            if (value != null)
            {
                this["UI_LLCC"] = value.Name;
            }
        }
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include preferred data language in the format described in [RFC1766] for cases where language can affect data calculation.
    /// </summary>
    public CultureInfo DcLlcc
    {
        get => new(this["DC_LLCC"]);
        set
        {
            if (value != null)
            {
                this["DC_LLCC"] = value.Name;
            }
        }
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to use the output of this action embedded in a web page.
    /// </summary>
    public bool Embedded
    {
        get => Convert.ToBoolean(this["EMBEDDED"], CultureInfo.InvariantCulture);
        set => this["EMBEDDED"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to prevent the attendee from navigating a file. For example, when using the attendee action (see st_wopi-action-values in section 3.1.5.1.1.2.3.1).
    /// </summary>
    public bool DisableAsync
    {
        get => Convert.ToBoolean(this["DISABLE_ASYNC"], CultureInfo.InvariantCulture);
        set => this["DISABLE_ASYNC"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to load a view of the document that does not create or join a broadcast session. This view looks and behaves like a regular broadcast frame.
    /// </summary>
    public bool DisableBroadcast
    {
        get => Convert.ToBoolean(this["DISABLE_BROADCAST"], CultureInfo.InvariantCulture);
        set => this["DISABLE_BROADCAST"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to load the file type in full-screen mode.
    /// </summary>
    public bool Fullscreen
    {
        get => Convert.ToBoolean(this["FULLSCREEN"], CultureInfo.InvariantCulture);
        set => this["FULLSCREEN"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to load the file type with a minimal user interface.
    /// </summary>
    public bool Recording
    {
        get => Convert.ToBoolean(this["RECORDING"], CultureInfo.InvariantCulture);
        set => this["RECORDING"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include a value to designate the theme used. Current values are "1" to indicate a light-colored theme and "2" to indicate a darker colored theme.
    /// </summary>
    public int ThemeId
    {
        get => Convert.ToInt32(this["THEME_ID"], CultureInfo.InvariantCulture);
        set => this["THEME_ID"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "1" to indicate that the user is a business user.
    /// </summary>
    public int BusinessUser
    {
        get => Convert.ToInt32(this["BUSINESS_USER"], CultureInfo.InvariantCulture);
        set => this["BUSINESS_USER"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "1" to load a view of the document that does not create or join a chat session.
    /// </summary>
    public int DisableChat
    {
        get => Convert.ToInt32(this["DISABLE_CHAT"], CultureInfo.InvariantCulture);
        set => this["DISABLE_CHAT"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Makes a purple, clickable box appear if you set it to 1.
    /// Sorry, this documentation hasn't been written yet. https://github.com/Microsoft/Office-Online-Test-Tools-and-Documentation/issues/52
    /// </summary>
    public int Perfstats
    {
        get => Convert.ToInt32(this["PERFSTATS"], CultureInfo.InvariantCulture);
        set => this["PERFSTATS"] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// This value can be passed by hosts to associate an Office for the web session with a host session identifier. This can help Office for the web engineers more quickly find logs for troubleshooting purposes based on a host-specific session identifier.
    /// </summary>
    public string HostSessionId
    {
        get => this["HOST_SESSION_ID"];
        set => this["HOST_SESSION_ID"] = value;
    }

    /// <summary>
    /// This placeholder can be replaced by any string value. If provided, this value will be passed back to the host in subsequent CheckFileInfo and CheckFolderInfo calls in the X-WOPI-SessionContext request header. There is no defined limit for the length of this string; however, since it is passed on the query string, it is subject to the overall Office for the web URL length limit of 2048 bytes.
    /// </summary>
    public string SessionContext
    {
        get => this["SESSION_CONTEXT"];
        set => this["SESSION_CONTEXT"] = value;
    }

    /// <summary>
    /// This placeholder must be replaced by a WopiSrc value. Unlike other placeholders, replacing this placeholder is required.
    /// New in version 2018.12.15: Prior to this version, hosts were required to add the WopiSrc to the action URL for most (but not all) actions. This placeholder enables hosts to handle the WopiSrc in the same way as other URL parameters.
    /// </summary>
    public string WopiSource
    {
        //TODO: unify with WopiSrc
        get => this["WOPI_SOURCE"];
        set => this["WOPI_SOURCE"] = value;
    }

    /// <summary>
    /// This value is used to run the WOPI Validation application in different modes.
    /// This value can be set to All, OfficeOnline or OfficeNativeClient to activate tests specific to Office Online and Office for iOS.If omitted, the default value is All.
    /// All: activates all WOPI Validation application tests.
    /// OfficeOnline: activates all tests necessary for Office Online integration.
    /// OfficeNativeClient: activates all tests necessary for Office for iOS integration.
    /// </summary>
    public ValidatorTestCategoryEnum ValidatorTestCategory
    {
        get
        {
            _ = Enum.TryParse(this["VALIDATOR_TEST_CATEGORY"], out ValidatorTestCategoryEnum validator);
            return validator;
        }
        set => this["VALIDATOR_TEST_CATEGORY"] = value.ToString();
    }

    /// <summary>
    /// Initializes an empty settings object.
    /// </summary>
    public WopiUrlSettings()
    {
    }

    /// <summary>
    /// Initializes the settings object with a dictionary representing setting keys and values.
    /// </summary>
    /// <param name="settings">A dictionary with key-value pairs representing settings.</param>
    public WopiUrlSettings(IDictionary<string, string> settings)
    {
        if (settings is not null)
        {
            foreach (var pair in settings)
            {
                Add(pair.Key, pair.Value);
            }
        }
    }
}
