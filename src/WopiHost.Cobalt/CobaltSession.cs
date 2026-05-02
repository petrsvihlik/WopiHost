using System.Security.Claims;
using Cobalt;
using Cobalt.Base.IO;
using WopiHost.Abstractions;

namespace WopiHost.Cobalt;

public class CobaltProcessor : ICobaltProcessor
{
    private readonly CoauthoringSessionTracker _sessionTracker = new();
    private async Task<CobaltFile> GetCobaltFile(IWopiFile file, ClaimsPrincipal principal)
    {
        var disposal = new DisposalEscrow(file.Owner);
        // CobaltCore 16.x retired `TemporaryHostBlobStore`; `LocalHostBlobStore` is the
        // closest equivalent (in-memory by default; can be backed by a directory if a
        // `dirPathForFileBackedBlobs` is supplied — left null here to match the old
        // temp-only behavior).
        var content = new CobaltFilePartitionConfig
        {
            IsNewFile = true,
            HostBlobStore = new LocalHostBlobStore(new LocalHostBlobStore.Config()),
            cellSchemaIsGenericFda = true,
            CellStorageConfig = new CellStorageConfig(),
            Schema = CobaltFilePartition.Schema.ShreddedCobalt,
            PartitionId = FilePartitionId.Content
        };

        var coauth = new CobaltFilePartitionConfig
        {
            IsNewFile = true,
            HostBlobStore = new LocalHostBlobStore(new LocalHostBlobStore.Config()),
            cellSchemaIsGenericFda = false,
            CellStorageConfig = new CellStorageConfig(),
            Schema = CobaltFilePartition.Schema.ShreddedCobalt,
            PartitionId = FilePartitionId.CoauthMetadata
        };

        var wacupdate = new CobaltFilePartitionConfig
        {
            IsNewFile = true,
            HostBlobStore = new LocalHostBlobStore(new LocalHostBlobStore.Config()),
            cellSchemaIsGenericFda = false,
            CellStorageConfig = new CellStorageConfig(),
            Schema = CobaltFilePartition.Schema.ShreddedCobalt,
            PartitionId = FilePartitionId.WordWacUpdate
        };

        var partitionConfigs = new Dictionary<FilePartitionId, CobaltFilePartitionConfig> { { FilePartitionId.Content, content }, { FilePartitionId.WordWacUpdate, wacupdate }, { FilePartitionId.CoauthMetadata, coauth } };

        // CobaltCore 16.x changed the CobaltFile ctor to take a
        // `GetConfigForPartitionAndVersion` delegate instead of a Dictionary, so the
        // host can serve different configurations per (partition, version) tuple.
        // Bridge the existing dictionary-based setup to the delegate by ignoring the
        // versionToken (we only have one config per partition).
        var tempCobaltFile = new CobaltFile(
            disposal,
            (partition, _) => partitionConfigs.TryGetValue(partition, out var cfg) ? cfg : null,
            new CobaltHostLockingStore(principal, file.Identifier, _sessionTracker),
            null);

        if (file.Exists)
        {
            using var stream = await file.GetReadStream();
            // 16.x made `AtomFromStream` an internal sealed type; the public surface
            // exposes `Atom.CreateFromArray(byte[])`. Buffer the stream so we can
            // hand a byte[] to the factory.
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var srcAtom = Atom.CreateFromArray(ms.ToArray());
            tempCobaltFile.GetCobaltFilePartition(FilePartitionId.Content).SetStream(RootId.Default.Value, srcAtom, out var o1);
            tempCobaltFile.GetCobaltFilePartition(FilePartitionId.Content).GetStream(RootId.Default.Value).Flush();
        }
        return tempCobaltFile;
    }

    /// <inheritdoc/>
    public async Task<byte[]> ProcessCobalt(IWopiFile file, ClaimsPrincipal principal, byte[] newContent)
    {
        // 16.x replaced the Atom-taking overload of `DeserializeInputFromProtocol` with one
        // that takes `CobaltStream`. Wrap the request bytes via `CobaltStream.Get(Stream)`.
        using var newContentStream = new MemoryStream(newContent, writable: false);
        var requestStream = CobaltStream.Get(newContentStream, streamIsImmutable: true);
        var requestBatch = new RequestBatch();

        requestBatch.DeserializeInputFromProtocol(requestStream, out var ctx, out var protocolVersion);
        var cobaltFile = await GetCobaltFile(file, principal);
        cobaltFile.CobaltEndpoint.ExecuteRequestBatch(requestBatch);

        if (requestBatch.Requests.Any(request => request is PutChangesRequest && request.PartitionId == FilePartitionId.Content))
        {
            using var stream = await file.GetWriteStream();
            new GenericFda(cobaltFile.CobaltEndpoint).GetContentStream().CopyTo(stream);
        }

        using var ms = new MemoryStream();
        requestBatch.SerializeOutputToProtocol(protocolVersion).CopyTo(ms);
        return ms.ToArray();
    }
}
