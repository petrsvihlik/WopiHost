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
    /// Wire framing used for MS-FSSHTTPB responses. cellbridge's SharePoint 2013 profile is what
    /// desktop Office has been verified against; switch to <see cref="FsshttpbSerializationProfile.Current"/>
    /// if a capture shows Office Online Server expecting the version 12 framing.
    /// </summary>
    public FsshttpbSerializationProfile SerializationProfile { get; set; } = FsshttpbSerializationProfile.SharePoint13_11;

    /// <summary>
    /// Value of the <c>WebUrl</c> attribute on SOAP responses. MS-FSSHTTP requires it to be same-origin
    /// with the request, which the <see cref="Abstractions.ICobaltProcessor"/> contract cannot observe;
    /// set it to the WOPI host's public origin when SOAP framing turns out to be in use.
    /// </summary>
    public string WebOrigin { get; set; } = string.Empty;
}
