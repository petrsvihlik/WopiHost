// Host-page side of the WOPI postMessage API: a thin transport over window.postMessage that the
// sample editor pages build their UI on. Office for the Web and Collabora share the same JSON
// envelope ({ MessageId, SendTime, Values }) and the same origin-scoping rules; the message *set*
// differs between them, so this module stays transport-only and leaves message semantics to the
// caller. Exposed as globals (no bundler) so a plain <script src> can use it from any sample.
//
// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/postmessage
// https://sdk.collaboraonline.com/docs/postmessage_api.html
(function (global) {
    'use strict';

    // Curated cross-client catalog of MessageId values for discoverability. Not exhaustive by
    // design: the host->editor commands and editor->host notifications each client supports
    // diverge and keep moving, so this lists the ones the sample exchanges plus those that map
    // to the documented *PostMessage host capability flags. The authoritative, per-client lists
    // live in the docs linked above.
    var WopiPostMessages = {
        // Host -> editor: lifecycle/control.
        Host_PostmessageReady: 'Host_PostmessageReady', // host is ready; until this lands the editor only answers this one message
        Grab_Focus: 'Grab_Focus',                       // ask the editor to take keyboard focus

        // Host -> editor: commands. Save/Print/Close are accepted by both Office and Collabora.
        Action_Save: 'Action_Save',                     // Values: { Notify, DontTerminateEdit, DontSaveIfUnmodified }
        Action_Print: 'Action_Print',
        Action_Close: 'Action_Close',                   // editor flushes pending saves, then replies Action_Close_Resp / UI_Close

        // Editor -> host: notifications.
        App_LoadingStatus: 'App_LoadingStatus',         // Values.Status: 'Frame_Ready' | 'Document_Loaded'
        Doc_ModifiedStatus: 'Doc_ModifiedStatus',       // Collabora; Values.Modified: bool
        Document_Modified: 'Document_Modified',         // Office for the Web equivalent of the above
        File_Rename: 'File_Rename',                     // Office for the Web; Values.NewName after a rename. Gated by SupportsRename in CheckFileInfo.

        // Editor -> host: UI affordances. Each corresponds to a *PostMessage flag the host
        // advertises in CheckFileInfo; the client (not the host) reads the flag and decides whether
        // to surface the affordance and emit the message. The trailing comment names that flag.
        UI_Sharing: 'UI_Sharing',                       // FileSharingPostMessage — editor's Share button
        UI_Close: 'UI_Close',                           // ClosePostMessage — editor's Close button
        UI_Edit: 'UI_Edit',                             // EditModePostMessage — switch from view to edit
        UI_SaveAs: 'UI_SaveAs',
        UI_FileEmbed: 'UI_FileEmbed',                   // FileEmbedCommandPostMessage — embed dialog
        UI_FileVersions: 'UI_FileVersions',             // FileVersionPostMessage — version history

        // Editor -> host: responses to Action_* commands (MessageId is the command + '_Resp').
        Action_Save_Resp: 'Action_Save_Resp',
        Action_Close_Resp: 'Action_Close_Resp'
    };

    // The wire envelope every WOPI client expects. Values defaults to {} so a bare command posts cleanly.
    function envelope(messageId, values) {
        return JSON.stringify({ MessageId: messageId, SendTime: Date.now(), Values: values || {} });
    }

    // `frame` may be a CSS selector, an element, or a getter — the iframe is often injected after
    // attach() runs (Detail.razor builds it from script), so it is resolved lazily on every use.
    function resolveFrame(frame) {
        if (!frame) return null;
        if (typeof frame === 'function') return frame();
        if (typeof frame === 'string') return document.querySelector(frame);
        return frame;
    }

    // Attaches host-page messaging to a WOPI editor iframe.
    //   options.frame        selector | element | getter for the editor iframe
    //   options.clientOrigin REQUIRED — the WOPI client's origin. Outgoing posts target it and
    //                        incoming messages from any other origin are dropped, so a stray frame
    //                        can neither drive nor observe the host.
    //   options.autoReady    announce Host_PostmessageReady on frame load (default true)
    function attach(options) {
        options = options || {};
        var clientOrigin = options.clientOrigin;
        if (!clientOrigin) throw new Error('WopiPostMessage.attach: clientOrigin is required.');

        var getFrame = function () { return resolveFrame(options.frame); };
        var handlers = {}; // MessageId -> [handler]

        function send(messageId, values) {
            var f = getFrame();
            if (!f || !f.contentWindow) return false;
            f.contentWindow.postMessage(envelope(messageId, values), clientOrigin);
            return true;
        }

        function on(messageId, handler) {
            (handlers[messageId] || (handlers[messageId] = [])).push(handler);
            return api;
        }

        function off(messageId, handler) {
            var list = handlers[messageId];
            if (!list) return api;
            if (!handler) { delete handlers[messageId]; return api; }
            var i = list.indexOf(handler);
            if (i >= 0) list.splice(i, 1);
            return api;
        }

        function onMessage(e) {
            if (e.origin !== clientOrigin) return;
            var msg;
            try { msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data; }
            catch (_) { return; }                 // non-JSON noise on the channel
            if (!msg || !msg.MessageId) return;
            var list = handlers[msg.MessageId];
            if (!list) return;                    // unregistered message: graceful no-op
            var values = msg.Values || {};
            for (var i = 0; i < list.length; i++) {
                // One handler throwing must not starve the others or kill the listener.
                try { list[i](values, msg); } catch (_) { /* swallow */ }
            }
        }

        // Collabora stays silent until the host announces readiness; Office for the Web also keys
        // its stream off Host_PostmessageReady. Announce on the frame's load, with a fallback timer
        // for the case where the frame finished loading before attach() wired the handler.
        function announceReady() { send(WopiPostMessages.Host_PostmessageReady, {}); }

        global.addEventListener('message', onMessage);

        if (options.autoReady !== false) {
            (function wireReady() {
                var f = getFrame();
                if (!f) { setTimeout(wireReady, 100); return; }
                f.addEventListener('load', announceReady);
                setTimeout(announceReady, 1500);
            })();
        }

        var api = {
            send: send,
            on: on,
            off: off,
            announceReady: announceReady,
            dispose: function () { global.removeEventListener('message', onMessage); handlers = {}; }
        };
        return api;
    }

    global.WopiPostMessage = { attach: attach };
    global.WopiPostMessages = WopiPostMessages;
})(window);
