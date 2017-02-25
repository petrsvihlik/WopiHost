namespace WopiHost.Core.Models
{
    public class HostCapabilities
    {

        public bool SupportsCoauth { get; set; }

        public bool SupportsCobalt { get; set; }

        public bool SupportsFolders { get; set; }

        public bool SupportsContainers { get; set; }

        public bool SupportsLocks { get; set; }

        public bool SupportsGetLock { get; set; }

        public bool SupportsExtendedLockLength { get; set; }

        public bool SupportsEcosystem { get; set; }

        public bool SupportsGetFileWopiSrc { get; set; }

        public bool SupportedShareUrlTypes { get; set; }

        public bool SupportsScenarioLinks { get; set; }

        public bool SupportsSecureStore { get; set; }

        public bool SupportsFileCreation { get; set; }

        public bool SupportsUpdate { get; set; }

        public bool SupportsRename { get; set; }

        public bool SupportsDeleteFile { get; set; }
        public bool SupportsUserInfo { get; set; }

    }
}
