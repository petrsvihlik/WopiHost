using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Cobalt;
using WopiHost.Abstractions;

namespace WopiHost.Core.Cobalt
{
    public class CobaltSession : AbstractEditSession
    {
        private DisposalEscrow _disposal;
        private Dictionary<FilePartitionId, CobaltFilePartitionConfig> _partitionConfigs;
        private CobaltFile _cobaltFile;

        private CobaltFile CobaltFile
        {
            get
            {
                if (_cobaltFile == null)
                {
                    var tempCobaltFile = new CobaltFile(Disposal, PartitionConfigs, new CobaltHostLockingStore(this), null);

                    if (File.Exists)
                    {
                        using (var stream = File.GetReadStream())
                        {
                            var srcAtom = new AtomFromStream(stream);
                            Metrics o1;
                            tempCobaltFile.GetCobaltFilePartition(FilePartitionId.Content).SetStream(RootId.Default.Value, srcAtom, out o1);
                            tempCobaltFile.GetCobaltFilePartition(FilePartitionId.Content).GetStream(RootId.Default.Value).Flush();
                            _cobaltFile = tempCobaltFile;
                        }
                    }
                }
                return _cobaltFile;
            }
        }

        private DisposalEscrow Disposal
        {
            get
            {
                return _disposal ?? (_disposal = new DisposalEscrow(SessionId));
            }
        }

        private Dictionary<FilePartitionId, CobaltFilePartitionConfig> PartitionConfigs
        {
            get
            {
                if (_partitionConfigs == null)
                {
                    CobaltFilePartitionConfig content = new CobaltFilePartitionConfig
                    {
                        IsNewFile = true,
                        HostBlobStore = new TemporaryHostBlobStore(new TemporaryHostBlobStore.Config(), Disposal, SessionId + @".Content"),
                        cellSchemaIsGenericFda = true,
                        CellStorageConfig = new CellStorageConfig(),
                        Schema = CobaltFilePartition.Schema.ShreddedCobalt,
                        PartitionId = FilePartitionId.Content
                    };

                    CobaltFilePartitionConfig coauth = new CobaltFilePartitionConfig
                    {
                        IsNewFile = true,
                        HostBlobStore = new TemporaryHostBlobStore(new TemporaryHostBlobStore.Config(), Disposal, SessionId + @".CoauthMetadata"),
                        cellSchemaIsGenericFda = false,
                        CellStorageConfig = new CellStorageConfig(),
                        Schema = CobaltFilePartition.Schema.ShreddedCobalt,
                        PartitionId = FilePartitionId.CoauthMetadata
                    };

                    CobaltFilePartitionConfig wacupdate = new CobaltFilePartitionConfig
                    {
                        IsNewFile = true,
                        HostBlobStore = new TemporaryHostBlobStore(new TemporaryHostBlobStore.Config(), Disposal, SessionId + @".WordWacUpdate"),
                        cellSchemaIsGenericFda = false,
                        CellStorageConfig = new CellStorageConfig(),
                        Schema = CobaltFilePartition.Schema.ShreddedCobalt,
                        PartitionId = FilePartitionId.WordWacUpdate
                    };

                    Dictionary<FilePartitionId, CobaltFilePartitionConfig> partitionConfs = new Dictionary<FilePartitionId, CobaltFilePartitionConfig> { { FilePartitionId.Content, content }, { FilePartitionId.WordWacUpdate, wacupdate }, { FilePartitionId.CoauthMetadata, coauth } };
                    _partitionConfigs = partitionConfs;
                }
                return _partitionConfigs;
            }
        }

        public CobaltSession(IWopiFile file, string sessionId, ClaimsPrincipal principal)
            : base(file, sessionId, principal)
        {
        }


        public override Stream GetFileStream()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new GenericFda(CobaltFile.CobaltEndpoint).GetContentStream().CopyTo(ms);
                return ms;
            }
        }

        public override byte[] GetFileContent()
        {
            var input = GetFileStream();
            MemoryStream ms;
            if (input is MemoryStream)
            {
                ms = (MemoryStream)input;
            }
            else
            {
                ms = new MemoryStream();
                input.CopyTo(ms);
            }
            return ms.ToArray();
        }

        public void Save()
        {
            lock (File)
            {
                using (var stream = File.GetWriteStream())
                {
                    new GenericFda(CobaltFile.CobaltEndpoint).GetContentStream().CopyTo(stream);
                }
            }
        }

        public void ExecuteRequestBatch(RequestBatch requestBatch)
        {
            CobaltFile.CobaltEndpoint.ExecuteRequestBatch(requestBatch);
            LastUpdated = DateTime.Now;
        }

        public override void Dispose()
        {
            // Save the changes to the file
            Save();

            Disposal.Dispose();
        }

        public override Action<Stream> SetFileContent(byte[] newContent)
        {
            // Refactoring tip: there are more ways of initializing Atom
            AtomFromByteArray atomRequest = new AtomFromByteArray(newContent);
            RequestBatch requestBatch = new RequestBatch();

            object ctx;
            ProtocolVersion protocolVersion;

            requestBatch.DeserializeInputFromProtocol(atomRequest, out ctx, out protocolVersion);
            ExecuteRequestBatch(requestBatch);

            if (requestBatch.Requests.Any(request => request.GetType() == typeof(PutChangesRequest) && request.PartitionId == FilePartitionId.Content))
            {
                Save();
            }
            var response = requestBatch.SerializeOutputToProtocol(protocolVersion);
            Action<Stream> copyToAction = s => { response.CopyTo(s); };
            return copyToAction;
        }
    }
}
