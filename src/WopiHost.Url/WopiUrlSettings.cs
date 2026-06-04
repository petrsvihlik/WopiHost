using System.Globalization;

namespace WopiHost.Url;

/// <summary>
/// Additional URL settings influencing the behavior of the WOPI client (as defined in https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery#placeholder-values).
/// Settings are used to replace the placeholders in URL templates retrieved from the WOPI discovery file.
/// </summary>
public class WopiUrlSettings : Dictionary<string, string>
{
    /// <summary>
    /// Names of the placeholder tokens that appear in WOPI discovery URL templates.
    /// Defined by the WOPI specification: see <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery#placeholder-values"/>.
    /// </summary>
    public static class Placeholders
    {
        /// <summary>The URL of the WOPI host's file endpoint, URL-escaped.</summary>
        public const string WopiSource = "WOPI_SOURCE";

        /// <summary>UI language tag (RFC 1766).</summary>
        public const string UiLlcc = "UI_LLCC";

        /// <summary>Data-calculation language tag (RFC 1766).</summary>
        public const string DcLlcc = "DC_LLCC";

        /// <summary>"true" to render the action embedded in another web page.</summary>
        public const string Embedded = "EMBEDDED";

        /// <summary>"true" to prevent attendees from navigating a file (broadcast attendee mode).</summary>
        public const string DisableAsync = "DISABLE_ASYNC";

        /// <summary>"true" to load a view that does not create or join a broadcast session.</summary>
        public const string DisableBroadcast = "DISABLE_BROADCAST";

        /// <summary>"true" to load the file type in full-screen mode.</summary>
        public const string Fullscreen = "FULLSCREEN";

        /// <summary>"true" to load the file type with a minimal user interface.</summary>
        public const string Recording = "RECORDING";

        /// <summary>"1" for light theme, "2" for dark theme.</summary>
        public const string ThemeId = "THEME_ID";

        /// <summary>"1" to indicate that the user is a business user.</summary>
        public const string BusinessUser = "BUSINESS_USER";

        /// <summary>"1" to disable chat in the editor session.</summary>
        public const string DisableChat = "DISABLE_CHAT";

        /// <summary>Set to "1" to display a Perfstats overlay (debugging aid).</summary>
        public const string Perfstats = "PERFSTATS";

        /// <summary>Host-supplied session identifier echoed in Office for the web logs.</summary>
        public const string HostSessionId = "HOST_SESSION_ID";

        /// <summary>Host-supplied opaque value echoed back via X-WOPI-SessionContext.</summary>
        public const string SessionContext = "SESSION_CONTEXT";

        /// <summary>Selects which WOPI Validator test suite to run (All, OfficeOnline, OfficeNativeClient).</summary>
        public const string ValidatorTestCategory = "VALIDATOR_TEST_CATEGORY";
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the preferred UI language in the format described in [RFC1766].
    /// </summary>
    public CultureInfo UiLlcc
    {
        get => new(this[Placeholders.UiLlcc]);
        set
        {
            if (value != null)
            {
                this[Placeholders.UiLlcc] = value.Name;
            }
        }
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include preferred data language in the format described in [RFC1766] for cases where language can affect data calculation.
    /// </summary>
    public CultureInfo DcLlcc
    {
        get => new(this[Placeholders.DcLlcc]);
        set
        {
            if (value != null)
            {
                this[Placeholders.DcLlcc] = value.Name;
            }
        }
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to use the output of this action embedded in a web page.
    /// </summary>
    public bool Embedded
    {
        get => Convert.ToBoolean(this[Placeholders.Embedded], CultureInfo.InvariantCulture);
        set => this[Placeholders.Embedded] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to prevent the attendee from navigating a file. For example, when using the attendee action (see st_wopi-action-values in section 3.1.5.1.1.2.3.1).
    /// </summary>
    public bool DisableAsync
    {
        get => Convert.ToBoolean(this[Placeholders.DisableAsync], CultureInfo.InvariantCulture);
        set => this[Placeholders.DisableAsync] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to load a view of the document that does not create or join a broadcast session. This view looks and behaves like a regular broadcast frame.
    /// </summary>
    public bool DisableBroadcast
    {
        get => Convert.ToBoolean(this[Placeholders.DisableBroadcast], CultureInfo.InvariantCulture);
        set => this[Placeholders.DisableBroadcast] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to load the file type in full-screen mode.
    /// </summary>
    public bool Fullscreen
    {
        get => Convert.ToBoolean(this[Placeholders.Fullscreen], CultureInfo.InvariantCulture);
        set => this[Placeholders.Fullscreen] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "true" to load the file type with a minimal user interface.
    /// </summary>
    public bool Recording
    {
        get => Convert.ToBoolean(this[Placeholders.Recording], CultureInfo.InvariantCulture);
        set => this[Placeholders.Recording] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include a value to designate the theme used. Current values are "1" to indicate a light-colored theme and "2" to indicate a darker colored theme.
    /// </summary>
    public int ThemeId
    {
        get => Convert.ToInt32(this[Placeholders.ThemeId], CultureInfo.InvariantCulture);
        set => this[Placeholders.ThemeId] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "1" to indicate that the user is a business user.
    /// </summary>
    public int BusinessUser
    {
        get => Convert.ToInt32(this[Placeholders.BusinessUser], CultureInfo.InvariantCulture);
        set => this[Placeholders.BusinessUser] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Indicates that the WOPI server MAY include the value "1" to load a view of the document that does not create or join a chat session.
    /// </summary>
    public int DisableChat
    {
        get => Convert.ToInt32(this[Placeholders.DisableChat], CultureInfo.InvariantCulture);
        set => this[Placeholders.DisableChat] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Makes a purple, clickable box appear when set to 1. Microsoft has not documented this value.
    /// </summary>
    public int Perfstats
    {
        get => Convert.ToInt32(this[Placeholders.Perfstats], CultureInfo.InvariantCulture);
        set => this[Placeholders.Perfstats] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// This value can be passed by hosts to associate an Office for the web session with a host session identifier. This can help Office for the web engineers more quickly find logs for troubleshooting purposes based on a host-specific session identifier.
    /// </summary>
    public string HostSessionId
    {
        get => this[Placeholders.HostSessionId];
        set => this[Placeholders.HostSessionId] = value;
    }

    /// <summary>
    /// This placeholder can be replaced by any string value. If provided, this value will be passed back to the host in subsequent CheckFileInfo and CheckFolderInfo calls in the X-WOPI-SessionContext request header. There is no defined limit for the length of this string; however, since it is passed on the query string, it is subject to the overall Office for the web URL length limit of 2048 bytes.
    /// </summary>
    public string SessionContext
    {
        get => this[Placeholders.SessionContext];
        set => this[Placeholders.SessionContext] = value;
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
            _ = Enum.TryParse(this[Placeholders.ValidatorTestCategory], out ValidatorTestCategoryEnum validator);
            return validator;
        }
        set => this[Placeholders.ValidatorTestCategory] = value.ToString();
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
    public WopiUrlSettings(IDictionary<string, string>? settings)
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
