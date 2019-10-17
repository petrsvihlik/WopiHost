using System;

namespace WopiHost.Discovery
{
    public class DiscoveryException : Exception
    {
        public DiscoveryException()
        {
        }

        public DiscoveryException(string message) : base(message)
        {
        }
        public DiscoveryException(string message, Exception originalException) : base(message, originalException)
        {
        }
    }
}
