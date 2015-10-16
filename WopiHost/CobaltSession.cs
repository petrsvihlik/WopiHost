using System;
using System.Collections.Generic;
using System.IO;
using Cobalt;
using System.Linq;
using WopiHost.Abstractions;

namespace WopiHost
{
	public class CobaltSession : EditSession
	{
		private CobaltFile m_cobaltFile;
		private CobaltFile CobaltFile
		{
			get
			{
				if (m_cobaltFile == null)
				{
					var tempCobaltFile = new CobaltFile(Disposal, PartitionConfs, new CobaltHostLockingStore(this), null);

					if (File.Exists)
					{
						using (var stream = File.GetReadStream())
						{
							var srcAtom = new AtomFromStream(stream);
							Metrics o1;
							tempCobaltFile.GetCobaltFilePartition(FilePartitionId.Content).SetStream(RootId.Default.Value, srcAtom, out o1);
							tempCobaltFile.GetCobaltFilePartition(FilePartitionId.Content).GetStream(RootId.Default.Value).Flush();
							m_cobaltFile = tempCobaltFile;
						}
					}
				}
				return m_cobaltFile;
			}
		}

		public override bool IsCobaltSession
		{
			get { return true; }
		}

		private DisposalEscrow _disposal;
		private Dictionary<FilePartitionId, CobaltFilePartitionConfig> _partitionConfs;

		private DisposalEscrow Disposal
		{
			get
			{
				return _disposal ?? (_disposal = new DisposalEscrow(SessionId));
			}
		}

		private Dictionary<FilePartitionId, CobaltFilePartitionConfig> PartitionConfs
		{
			get
			{
				if (_partitionConfs == null)
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
					_partitionConfs = partitionConfs;
				}
				return _partitionConfs;
			}
		}

		public CobaltSession(IWopiFile file, string sessionId, string login = "Anonymous", string name = "Anonymous", string email = "", bool isAnonymous = true)
			: base(file, sessionId, login, name, email, isAnonymous)
		{

		}
		public override byte[] GetFileContent()
		{
			MemoryStream ms = new MemoryStream();
			new GenericFda(CobaltFile.CobaltEndpoint).GetContentStream().CopyTo(ms);
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
