using CellBridge.FssHttpB;

namespace WopiHost.CellBridge;

/// <summary>
/// Settings for <see cref="CellBridgeProcessor"/>, bound from the <c>Wopi:CellBridge</c> section.
/// </summary>
public sealed class CellBridgeProcessorOptions
{
    /// <summary>Configuration section the options bind from.</summary>
    public const string SectionName = "Wopi:CellBridge";

    /// <summary>
    /// Wire framing used for responses to a raw MS-FSSHTTPB body; cell responses inside a SOAP envelope
    /// always use cellbridge's SharePoint 2013 profile. That profile is what
    /// desktop Office has been verified against; switch to <see cref="FsshttpbSerializationProfile.Current"/>
    /// if a capture shows Office Online Server expecting the version 12 framing.
    /// </summary>
    public FsshttpbSerializationProfile SerializationProfile { get; set; } = FsshttpbSerializationProfile.SharePoint13_11;

    /// <summary>
    /// Public origin (<c>https://host[:port]</c>) reported as the <c>WebUrl</c> of SOAP responses, which
    /// MS-FSSHTTP requires to be same-origin with the request. Empty means the current request's origin;
    /// set it when the host sits behind a proxy that rewrites scheme or host.
    /// </summary>
    public string WebOrigin { get; set; } = string.Empty;
}
