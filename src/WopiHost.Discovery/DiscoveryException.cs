namespace WopiHost.Discovery;

/// <summary>
/// An exception that might occur during retrieval of the WOPI discovery file.
/// </summary>
public class DiscoveryException : Exception
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    public DiscoveryException()
    {
    }

    /// <summary>
    /// Initializes the <see cref="DiscoveryException"/> with a message.
    /// </summary>
    /// <param name="message">Error message.</param>
    public DiscoveryException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes the <see cref="DiscoveryException"/> with a message and the original exception.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="originalException">The original exception that caused the problem.</param>
    public DiscoveryException(string message, Exception originalException) : base(message, originalException)
    {
    }
}
