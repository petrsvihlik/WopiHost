namespace WopiHost.Abstractions
{
    /// <summary>
    /// Configuration class for WopiHost.Core
    /// </summary>
    public class WopiHostOptions
    {
        public string WopiRootPath { get; set; }

        public string WopiFileProviderAssemblyName { get; set; }

        public string WebRootPath { get; set; }
    }
}
