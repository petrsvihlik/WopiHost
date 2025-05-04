namespace WopiHost.Web.Models;

/// <summary>
/// Represents an error that occurs in the application.
/// </summary>
public class ErrorViewModel
{
    /// <summary>
    /// Gets or sets the exception that caused the error.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show the exception details.
    /// </summary>
    public bool ShowExceptionDetails { get; set; }
} 