using System.Globalization;

namespace WopiHost.Url;

/// <summary>
/// Represents URL settings that influence the behavior of the WOPI client, keyed by placeholder name.
/// </summary>
/// <remarks>
/// The settings replace the placeholders in URL templates retrieved from the WOPI discovery file.
/// </remarks>
/// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery#placeholder-values">Placeholder values</seealso>
public class WopiUrlSettings : Dictionary<string, string>
{
    /// <summary>
    /// Provides the names of the placeholder tokens that appear in WOPI discovery URL templates.
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/discovery#placeholder-values">Placeholder values</seealso>
    public static class Placeholders
    {
        /// <summary>The placeholder for the URL of the WOPI host's file endpoint, URL-escaped.</summary>
        public const string WopiSource = "WOPI_SOURCE";

        /// <summary>The placeholder for the UI language tag (RFC 1766).</summary>
        public const string UiLlcc = "UI_LLCC";

        /// <summary>The placeholder for the data-calculation language tag (RFC 1766).</summary>
        public const string DcLlcc = "DC_LLCC";

        /// <summary>The placeholder that, set to <c>true</c>, renders the action embedded in another web page.</summary>
        public const string Embedded = "EMBEDDED";

        /// <summary>The placeholder that, set to <c>true</c>, prevents attendees from navigating a file (broadcast attendee mode).</summary>
        public const string DisableAsync = "DISABLE_ASYNC";

        /// <summary>The placeholder that, set to <c>true</c>, loads a view that does not create or join a broadcast session.</summary>
        public const string DisableBroadcast = "DISABLE_BROADCAST";

        /// <summary>The placeholder that, set to <c>true</c>, loads the file type in full-screen mode.</summary>
        public const string Fullscreen = "FULLSCREEN";

        /// <summary>The placeholder that, set to <c>true</c>, loads the file type with a minimal user interface.</summary>
        public const string Recording = "RECORDING";

        /// <summary>The placeholder for the theme: <c>1</c> for light, <c>2</c> for dark.</summary>
        public const string ThemeId = "THEME_ID";

        /// <summary>The placeholder that, set to <c>1</c>, indicates that the user is a business user.</summary>
        public const string BusinessUser = "BUSINESS_USER";

        /// <summary>The placeholder that, set to <c>1</c>, disables chat in the editor session.</summary>
        public const string DisableChat = "DISABLE_CHAT";

        /// <summary>The placeholder that, set to <c>1</c>, displays a Perfstats overlay (debugging aid).</summary>
        public const string Perfstats = "PERFSTATS";

        /// <summary>The placeholder for a host-supplied session identifier echoed in Office for the web logs.</summary>
        public const string HostSessionId = "HOST_SESSION_ID";

        /// <summary>The placeholder for a host-supplied opaque value echoed back in the <c>X-WOPI-SessionContext</c> header.</summary>
        public const string SessionContext = "SESSION_CONTEXT";

        /// <summary>The placeholder that selects which WOPI Validator test suite runs; see <see cref="ValidatorTestCategoryEnum"/>.</summary>
        public const string ValidatorTestCategory = "VALIDATOR_TEST_CATEGORY";
    }

    /// <summary>
    /// Gets or sets the preferred UI language (<c>UI_LLCC</c>, RFC 1766 format).
    /// Returns <see langword="null"/> when the placeholder has not been set. Setting <see langword="null"/> leaves an existing value unchanged.
    /// </summary>
    public CultureInfo? UiLlcc
    {
        get => TryGetValue(Placeholders.UiLlcc, out var value) ? new CultureInfo(value) : null;
        set
        {
            if (value != null)
            {
                this[Placeholders.UiLlcc] = value.Name;
            }
        }
    }

    /// <summary>
    /// Gets or sets the preferred data language (<c>DC_LLCC</c>, RFC 1766 format), used where language can affect data calculation.
    /// Returns <see langword="null"/> when the placeholder has not been set. Setting <see langword="null"/> leaves an existing value unchanged.
    /// </summary>
    public CultureInfo? DcLlcc
    {
        get => TryGetValue(Placeholders.DcLlcc, out var value) ? new CultureInfo(value) : null;
        set
        {
            if (value != null)
            {
                this[Placeholders.DcLlcc] = value.Name;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the output of the action is embedded in a web page (<c>EMBEDDED</c>).
    /// Returns <see langword="false"/> when the placeholder has not been set.
    /// </summary>
    public bool Embedded
    {
        get => TryGetValue(Placeholders.Embedded, out var value) && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        set => this[Placeholders.Embedded] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the attendee is prevented from navigating a file, for example with the
    /// <c>attend</c> action (<c>DISABLE_ASYNC</c>).
    /// Returns <see langword="false"/> when the placeholder has not been set.
    /// </summary>
    public bool DisableAsync
    {
        get => TryGetValue(Placeholders.DisableAsync, out var value) && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        set => this[Placeholders.DisableAsync] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to load a view of the document that does not create or join a broadcast
    /// session but looks and behaves like a regular broadcast frame (<c>DISABLE_BROADCAST</c>).
    /// Returns <see langword="false"/> when the placeholder has not been set.
    /// </summary>
    public bool DisableBroadcast
    {
        get => TryGetValue(Placeholders.DisableBroadcast, out var value) && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        set => this[Placeholders.DisableBroadcast] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to load the file type in full-screen mode (<c>FULLSCREEN</c>).
    /// Returns <see langword="false"/> when the placeholder has not been set.
    /// </summary>
    public bool Fullscreen
    {
        get => TryGetValue(Placeholders.Fullscreen, out var value) && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        set => this[Placeholders.Fullscreen] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to load the file type with a minimal user interface (<c>RECORDING</c>).
    /// Returns <see langword="false"/> when the placeholder has not been set.
    /// </summary>
    public bool Recording
    {
        get => TryGetValue(Placeholders.Recording, out var value) && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        set => this[Placeholders.Recording] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets the theme (<c>THEME_ID</c>): <c>1</c> for a light-colored theme, <c>2</c> for a darker colored theme.
    /// Returns 0 when the placeholder has not been set.
    /// </summary>
    public int ThemeId
    {
        get => TryGetValue(Placeholders.ThemeId, out var value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        set => this[Placeholders.ThemeId] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets the business-user flag (<c>BUSINESS_USER</c>): <c>1</c> indicates that the user is a business user.
    /// Returns 0 when the placeholder has not been set.
    /// </summary>
    public int BusinessUser
    {
        get => TryGetValue(Placeholders.BusinessUser, out var value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        set => this[Placeholders.BusinessUser] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets the chat flag (<c>DISABLE_CHAT</c>): <c>1</c> loads a view of the document that does not create or join a chat session.
    /// Returns 0 when the placeholder has not been set.
    /// </summary>
    public int DisableChat
    {
        get => TryGetValue(Placeholders.DisableChat, out var value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        set => this[Placeholders.DisableChat] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets the Perfstats flag (<c>PERFSTATS</c>): <c>1</c> makes a purple, clickable box appear. Microsoft has not documented this value.
    /// Returns 0 when the placeholder has not been set.
    /// </summary>
    public int Perfstats
    {
        get => TryGetValue(Placeholders.Perfstats, out var value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        set => this[Placeholders.Perfstats] = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets or sets a host session identifier associated with the Office for the web session (<c>HOST_SESSION_ID</c>),
    /// which helps Office for the web engineers find logs for troubleshooting.
    /// Returns <see langword="null"/> when the placeholder has not been set. Setting <see langword="null"/> leaves an existing value unchanged.
    /// </summary>
    public string? HostSessionId
    {
        get => TryGetValue(Placeholders.HostSessionId, out var value) ? value : null;
        set
        {
            if (value != null)
            {
                this[Placeholders.HostSessionId] = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets an arbitrary string (<c>SESSION_CONTEXT</c>) passed back to the host in subsequent CheckFileInfo and
    /// CheckFolderInfo calls in the <c>X-WOPI-SessionContext</c> request header.
    /// Returns <see langword="null"/> when the placeholder has not been set. Setting <see langword="null"/> leaves an existing value unchanged.
    /// </summary>
    /// <remarks>
    /// The length is not limited by the spec, but the value travels on the query string and so counts toward the
    /// Office for the web URL length limit of 2048 bytes.
    /// </remarks>
    public string? SessionContext
    {
        get => TryGetValue(Placeholders.SessionContext, out var value) ? value : null;
        set
        {
            if (value != null)
            {
                this[Placeholders.SessionContext] = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the WOPI Validator test suite to run (<c>VALIDATOR_TEST_CATEGORY</c>).
    /// Returns <see cref="ValidatorTestCategoryEnum.All"/> when the placeholder has not been set.
    /// </summary>
    public ValidatorTestCategoryEnum ValidatorTestCategory
    {
        get
        {
            _ = TryGetValue(Placeholders.ValidatorTestCategory, out var value);
            _ = Enum.TryParse(value, out ValidatorTestCategoryEnum validator);
            return validator;
        }
        set => this[Placeholders.ValidatorTestCategory] = value.ToString();
    }

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="WopiUrlSettings"/> class.
    /// </summary>
    public WopiUrlSettings()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WopiUrlSettings"/> class that contains the specified settings.
    /// </summary>
    /// <param name="settings">The placeholder names and values to copy, or <see langword="null"/> for none.</param>
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
