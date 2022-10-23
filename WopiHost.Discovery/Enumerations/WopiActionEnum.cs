namespace WopiHost.Discovery.Enumerations;

/// <summary>
/// All possible WOPI actions implemented with regards to the https://msdn.microsoft.com/en-us/library/hh695254.aspx
/// </summary>
public enum WopiActionEnum
{
    /// <summary>
    /// An action that renders a non-editable view of a document.
    /// </summary>
    View,

    /// <summary>
    /// An action that allows users to edit a document.
    /// Requires update, locks
    /// </summary>
    Edit,

    /// <summary>
    /// An action that creates a new document using a blank file template appropriate to the file type, then opens that file for editing in Office for the web.
    /// Requires update, locks
    /// </summary>
    EditNew,

    /// <summary>
    /// An action that converts a document in a binary format, such as doc, into a modern format, like docx, so that it can be edited in Office for the web.
    /// Requires update, locks
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
    /// An action that supports editing the file type in a mode better suited to working with files that have been used to collect form data via the formsubmit action.
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
    /// This action provides the location of a broadcast endpoint for broadcast presenters.
		/// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    PresentService,

    /// <summary>
    /// An action that attends a broadcast of a document.
		/// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Attend,

    /// <summary>
    /// This action provides the location of a broadcast endpoint for broadcast attendees.
		/// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    AttendService,

    /// <summary>
    /// An action used to preload static content for Office for the web edit applications.
    /// </summary>
    PreloadEdit,

    /// <summary>
    /// An action used to preload static content for Office for the web edit applications.
    /// </summary>
    PreloadView,

    /// <summary>
		/// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Syndicate,

    /// <summary>
		/// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    LegacyWebService,

    /// <summary>
    /// 
		/// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Rtc,

    /// <summary>
		/// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    Collab,

    /// <summary>
    /// Not supported within the Office 365 - Cloud Storage Partner Program.
    /// </summary>
    DocumentChat
}