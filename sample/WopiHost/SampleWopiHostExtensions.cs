using WopiHost.Abstractions;

namespace WopiHost;

/// <summary>
/// Sample <see cref="IWopiHostExtensions"/> that advertises the host page's postMessage
/// capabilities. The paired front end (WopiHost.Web's <c>WopiLayout</c>) handles the matching
/// editor-to-host messages: <c>UI_Sharing</c> (Share), <c>UI_Close</c> (Close), and <c>UI_Edit</c>
/// (the view&#8596;edit switch).
/// </summary>
/// <remarks>
/// These are advertisements, not server-side checks: the WOPI client reads them from
/// <c>CheckFileInfo</c> and decides whether to surface the affordance and emit the message.
/// Office for the Web additionally posts only to <see cref="WopiCheckFileInfo.PostMessageOrigin"/>,
/// so it would need that set to the front-end origin; Collabora (the dev-loop client) ignores the
/// field, so it is left unset here.
/// </remarks>
public class SampleWopiHostExtensions : WopiHostExtensions
{
    /// <inheritdoc />
    public override Task<WopiCheckFileInfo> OnCheckFileInfoAsync(WopiCheckFileInfoContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var checkFileInfo = context.CheckFileInfo;
        checkFileInfo.FileSharingPostMessage = true;
        checkFileInfo.ClosePostMessage = true;
        checkFileInfo.EditModePostMessage = true;
        return Task.FromResult(checkFileInfo);
    }
}
