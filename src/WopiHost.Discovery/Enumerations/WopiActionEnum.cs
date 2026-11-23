namespace WopiHost.Discovery.Enumerations;

/// <summary>
/// Specifies the WOPI actions a WOPI client can advertise in its discovery XML.
/// </summary>
/// <seealso href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/429749b7-5ec3-4553-a589-0ec5240121ad">MS-WOPI: ST_wopi-action-values</seealso>
public enum WopiActionEnum
{
    /// <summary>
    /// An action that renders a non-editable view of a document.
    /// </summary>
    View,

    /// <summary>
    /// An action that allows users to edit a document.
    /// Requires the <c>update</c> and <c>locks</c> host capabilities.
    /// </summary>
    Edit,

    /// <summary>
    /// An action that creates a new document using a blank file template appropriate to the file type, then opens that file for editing in Office for the web.
    /// Requires the <c>update</c> and <c>locks</c> host capabilities.
    /// </summary>
    EditNew,

    /// <summary>
    /// An action that converts a document in a binary format, such as doc, into a modern format, like docx, so that it can be edited in Office for the web.
    /// Requires the <c>update</c> and <c>locks</c> host capabilities.
    /// </summary>
    Convert,

    /// <summary>
    /// An action that returns a set of URLs that can be used to execute automated test cases.
    /// </summary>
    GetInfo,

    /// <summary>
    /// An action that provides an interactive preview of the file type.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    InteractivePreview,

    /// <summary>
    /// An action that renders a non-editable view of a document that is optimized for viewing on mobile devices such as smartphones.
    /// </summary>
    MobileView,

    /// <summary>
    /// An action that renders a non-editable view of a document that is optimized for embedding in a web page.
    /// </summary>
    EmbedView,

    /// <summary>
    /// An action that provides a static image preview of the file type.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    ImagePreview,

    /// <summary>
    /// An action that supports accepting changes to the file type via a form-style interface.
    /// </summary>
    FormSubmit,

    /// <summary>
    /// An action that supports editing the file type in a mode better suited to working with files that have been used to collect form data via the <c>formsubmit</c> action.
    /// </summary>
    FormEdit,

    /// <summary>
    /// An action that supports interacting with the file type via additional URL parameters that are specific to the file type in question.
    /// </summary>
    Rest,

    /// <summary>
    /// An action that presents a broadcast of a document.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Present,

    /// <summary>
    /// An action that provides the location of a broadcast endpoint for broadcast presenters.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    PresentService,

    /// <summary>
    /// An action that attends a broadcast of a document.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Attend,

    /// <summary>
    /// An action that provides the location of a broadcast endpoint for broadcast attendees.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    AttendService,

    /// <summary>
    /// An action used to preload static content for Office for the web edit applications.
    /// </summary>
    PreloadEdit,

    /// <summary>
    /// An action used to preload static content for Office for the web view applications.
    /// </summary>
    PreloadView,

    /// <summary>
    /// The <c>syndicate</c> discovery action.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Syndicate,

    /// <summary>
    /// The <c>legacywebservice</c> discovery action.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    LegacyWebService,

    /// <summary>
    /// The <c>rtc</c> discovery action.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Rtc,

    /// <summary>
    /// The <c>collab</c> discovery action.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Collab,

    /// <summary>
    /// The <c>documentchat</c> discovery action.
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    DocumentChat,

    /// <summary>
    /// An action that opens the file, letting the WOPI client pick the view or edit experience itself.
    /// </summary>
    Open,

    /// <summary>
    /// An action that allows users to edit a document in a mode optimized for embedding in a web page.
    /// </summary>
    EmbedEdit,

    /// <summary>
    /// An action that renders a preview of the file optimized for embedding in a web page.
    /// </summary>
    EmbedPreview,

    /// <summary>
    /// An action that renders a UI for configuring how the file is embedded in a web page.
    /// </summary>
    EmbedConfigurator,

    /// <summary>
    /// An action that previews the file as it appears to a user filling in a form.
    /// </summary>
    FormPreview,

    /// <summary>
    /// An action used to preload static content for the Office for the web unified application.
    /// </summary>
    PreloadUnifiedApp
}