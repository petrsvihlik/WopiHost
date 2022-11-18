namespace WopiHost.Abstractions;

/// <summary>
/// Constants with configuration section names.
/// </summary>
public class WopiConfigurationSections
{
    /// <summary>
    /// Name of the configuration section.
    /// </summary>
    public const string WOPI_ROOT = "Wopi";

    /// <summary>
    /// Name of the configuration section related to storage settings.
    /// </summary>
    public const string STORAGE_OPTIONS = WOPI_ROOT + ":StorageProvider";

    /// <summary>
    /// Name of the configuration section related to WOPI discovery.
    /// </summary>
    public const string DISCOEVRY_OPTIONS = WOPI_ROOT + ":Discovery";
}
