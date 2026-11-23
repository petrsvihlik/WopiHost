namespace WopiHost.Discovery;

/// <summary>
/// Represents an error that occurs while retrieving or parsing the WOPI discovery file.
/// </summary>
public class DiscoveryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryException"/> class.
    /// </summary>
    public DiscoveryException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DiscoveryException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryException"/> class with a specified error message
    /// and the exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="originalException">The exception that is the cause of the current exception.</param>
    public DiscoveryException(string message, Exception originalException) : base(message, originalException)
    {
    }
}
