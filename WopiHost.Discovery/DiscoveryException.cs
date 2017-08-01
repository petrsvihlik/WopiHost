using System;

namespace WopiHost.Discovery
{
    public class DiscoveryException : Exception
    {
        public DiscoveryException(string message, Exception originalException) : base(message, originalException)
        {
        }
    }
}
