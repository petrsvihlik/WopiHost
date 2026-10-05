using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using CellBridge.AspNetCore;
using CellBridge.FssHttp;
using CellBridge.FssHttpB;
using CellBridge.Storage;
using CellBridge.Storage.Abstractions;
using CellBridge.Storage.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.CellBridge;

/// <summary>
/// MS-FSSHTTP processor backed by the open-source cellbridge engine instead of the proprietary
/// <c>Microsoft.CobaltCore</c>.
/// </summary>
/// <remarks>
/// <para>
/// cellbridge keeps protocol state (storage index, data-element graph, knowledge, editor sessions,
/// locks) in a <see cref="StorageProvider"/>. This processor uses the in-memory stores, the same
/// posture as <c>WopiHost.Cobalt</c>'s in-memory <c>LocalHostBlobStore</c>: each WOPI file is imported
/// into cellbridge on first use and the materialized document is written back through
/// <see cref="IWopiWritableFile.OpenWriteAsync"/> whenever a save advances the content version.
/// </para>
/// <para>
/// The WOPI <c>COBALT</c> override hands over the request body without its Content-Type. cellbridge
/// speaks SOAP (what desktop Office sends to SharePoint's <c>cellstorage.svc</c>) and raw MS-FSSHTTPB
/// (the binary cell request carried inside a SOAP <c>Cell</c> subrequest). Which framing Office Online
/// Server uses over WOPI has not been captured yet, so both are accepted and anything else fails
/// loudly instead of answering garbage.
/// </para>
/// </remarks>
public sealed partial class CellBridgeProcessor : ICobaltProcessor, IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> s_noAttributes = new Dictionary<string, string>(StringComparer.Ordinal);

    // Imports run on behalf of the host, not the editing user: ownership is recorded for the user,
    // creation permission for the host.
    private static readonly CellBridgeActor s_importer = new(new SubjectIdentity("wopi:host", "wopi-host", "WOPI host"), CanCreate: true);

    private readonly ILogger<CellBridgeProcessor> _logger;
    private readonly CellBridgeProcessorOptions _options;
    private readonly StorageProvider _storage;
    private readonly CellBridgeDocumentService _documents;
    private readonly ConcurrentDictionary<string, Lazy<Task<Guid>>> _resources = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writeLocks = new(StringComparer.Ordinal);
    private int _disposed;

    public CellBridgeProcessor(ILogger<CellBridgeProcessor> logger, IOptions<CellBridgeProcessorOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _storage = new StorageProvider(new InMemoryStateStore(), new InMemoryContentStore());
        _documents = new CellBridgeDocumentService(_storage, new WopiCellBridgeAccessEvaluator());
    }

    /// <inheritdoc/>
    public async Task<byte[]> ProcessCobalt(IWopiWritableFile file, ClaimsPrincipal principal, byte[] newContent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(newContent);

        var actor = ActorFor(principal);
        var resourceId = await GetOrImportAsync(file, actor, cancellationToken).ConfigureAwait(false);

        switch (WopiCobaltFraming.Detect(newContent))
        {
            case WopiCobaltFraming.Kind.Fsshttpb:
                return await ExecuteBinaryAsync(file, resourceId, actor, newContent, cancellationToken).ConfigureAwait(false);
            case WopiCobaltFraming.Kind.Soap:
                return await ExecuteSoapAsync(file, resourceId, actor, newContent, cancellationToken).ConfigureAwait(false);
            default:
                LogUnknownFraming(_logger, file.Identifier, Convert.ToHexString(newContent.AsSpan(0, Math.Min(newContent.Length, 16))));
                throw new NotSupportedException(
                    "The COBALT request body is neither a raw MS-FSSHTTPB cell request nor a SOAP cell storage request. " +
                    "Capture the body and see src/WopiHost.CellBridge/README.md.");
        }
    }

    private async Task<byte[]> ExecuteBinaryAsync(IWopiWritableFile file, Guid resourceId, CellBridgeActor actor, byte[] body, CancellationToken cancellationToken)
    {
        var request = FsshttpbCellRequest.Deserialize(new BinaryReaderEx(body));
        var kind = DocumentPartitionKind.FileContents;
        var target = request.SubRequests.Select(s => s.TargetPartitionId).FirstOrDefault(t => t.HasValue);
        if (target is { } partitionId && !CellPartitionSelector.TryResolve(partitionId, out kind))
        {
            throw new NotSupportedException($"Unknown MS-FSSHTTPB target partition {partitionId}.");
        }

        var execution = await ExecuteCellAsync(file, resourceId, kind, request, s_noAttributes, actor, cancellationToken).ConfigureAwait(false);
        return execution.Response.ToByteArray(_options.SerializationProfile);
    }

    private async Task<byte[]> ExecuteSoapAsync(IWopiWritableFile file, Guid resourceId, CellBridgeActor actor, byte[] body, CancellationToken cancellationToken)
    {
        var request = CellStorageRequestParser.Parse(Encoding.UTF8.GetString(body));
        var response = new CellStorageResponse
        {
            Version = request.Version,
            // SharePoint answers a 2.x request with the highest minor revision it supports.
            MinorVersion = request.Version == 2 ? 3u : request.MinorVersion,
            UsesDirectBody = request.UsesDirectBody,
            WebUrl = _options.WebOrigin,
        };

        foreach (var fileRequest in request.Requests)
        {
            var fileResponse = new FssHttpResponse
            {
                Url = fileRequest.Url,
                RequestToken = fileRequest.RequestToken,
                IntervalOverride = 0,
                ResourceId = resourceId,
            };

            foreach (var subRequest in fileRequest.SubRequests)
            {
                var subResponse = new FssHttpSubResponse
                {
                    Type = subRequest.Type,
                    SubRequestToken = subRequest.SubRequestToken,
                    ErrorCode = "Success",
                    HResult = "0",
                };

                if (FssHttpDependencies.Error(subRequest, fileResponse.SubResponses) is { } dependencyError)
                {
                    subResponse.ErrorCode = dependencyError;
                    subResponse.HResult = "2147500037";
                    subResponse.EmitEmptySubResponseData = true;
                }
                else
                {
                    await ApplySubRequestAsync(file, resourceId, actor, subRequest, subResponse, cancellationToken).ConfigureAwait(false);
                }

                fileResponse.SubResponses.Add(subResponse);
            }

            response.Responses.Add(fileResponse);
        }

        return Encoding.UTF8.GetBytes(response.ToSoapEnvelope());
    }

    private async Task ApplySubRequestAsync(IWopiWritableFile file, Guid resourceId, CellBridgeActor actor, FssHttpSubRequest subRequest, FssHttpSubResponse subResponse, CancellationToken cancellationToken)
    {
        switch (subRequest.Type)
        {
            case SubRequestType.Cell:
                await ApplyCellAsync(file, resourceId, actor, subRequest, subResponse, cancellationToken).ConfigureAwait(false);
                break;

            case SubRequestType.WhoAmI:
                subResponse.SubResponseDataAttributes["UserName"] = actor.Identity.DisplayName;
                subResponse.SubResponseDataAttributes["UserLogin"] = actor.Identity.Login;
                break;

            case SubRequestType.ServerTime:
                subResponse.SubResponseDataAttributes["ServerTime"] = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
                break;

            case SubRequestType.SchemaLock:
            case SubRequestType.ExclusiveLock:
            case SubRequestType.LockStatus:
            case SubRequestType.AmIAlone:
            case SubRequestType.Coauth:
                await ApplyCoordinationAsync(resourceId, actor, subRequest, subResponse, cancellationToken).ConfigureAwait(false);
                break;

            default:
                // EditorsTable, GetDocMetaInfo and GetVersions are implemented inside cellbridge's
                // own HTTP endpoint and not yet exposed as reusable services.
                subResponse.ErrorCode = "NotSupported";
                break;
        }
    }

    private async Task ApplyCellAsync(IWopiWritableFile file, Guid resourceId, CellBridgeActor actor, FssHttpSubRequest subRequest, FssHttpSubResponse subResponse, CancellationToken cancellationToken)
    {
        if (!CellPartitionSelector.TryResolve(subRequest.SubRequestDataAttributes, out var kind))
        {
            subResponse.ErrorCode = "InvalidArgument";
            subResponse.HResult = "2147942487";
            return;
        }

        var payload = DecodeCellPayload(subRequest);
        if (payload is null)
        {
            subResponse.ErrorCode = "InvalidArgument";
            return;
        }

        var execution = await ExecuteCellAsync(file, resourceId, kind, payload, subRequest.SubRequestDataAttributes, actor, cancellationToken).ConfigureAwait(false);
        if (execution.LockError is { } lockError)
        {
            subResponse.ErrorCode = lockError;
            return;
        }

        subResponse.SubResponseDataBase64 = execution.Response.ToByteArray(_options.SerializationProfile);
    }

    // SOAP carries the binary cell request either as an MTOM part (already resolved into
    // SubRequestDataBinaryMemory by the parser) or as base64 text inside SubRequestData.
    private static FsshttpbCellRequest? DecodeCellPayload(FssHttpSubRequest subRequest)
    {
        try
        {
            if (subRequest.SubRequestDataBinaryMemory is { } binary)
            {
                return FsshttpbCellRequest.Deserialize(new BinaryReaderEx(binary));
            }

            if (subRequest.SubRequestDataXml is { } xml)
            {
                var text = XDocument.Parse(xml).Root?.Value;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return FsshttpbCellRequest.Deserialize(new BinaryReaderEx(Convert.FromBase64String(text.Trim())));
                }
            }
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or EndOfStreamException or ArgumentException or System.Xml.XmlException)
        {
            return null;
        }

        return null;
    }

    private Task ApplyCoordinationAsync(Guid resourceId, CellBridgeActor actor, FssHttpSubRequest subRequest, FssHttpSubResponse subResponse, CancellationToken cancellationToken) =>
        _storage.State.TransitionAsync(resourceId, (current, now) =>
        {
            var document = StoredDocument.RestoreMetadata(current, now);
            var coordinator = FssHttpLockCoordinator.Restore(document, current.Coordination, now, actor.Identity);
            switch (subRequest.Type)
            {
                case SubRequestType.SchemaLock:
                    coordinator.ApplySchemaLock(subRequest, subResponse, now);
                    break;
                case SubRequestType.ExclusiveLock:
                    coordinator.ApplyExclusiveLock(subRequest, subResponse, now);
                    break;
                case SubRequestType.LockStatus:
                    coordinator.ApplyLockStatus(subRequest, subResponse, now);
                    break;
                case SubRequestType.AmIAlone:
                    coordinator.ApplyAmIAlone(document, subRequest, subResponse);
                    break;
                case SubRequestType.Coauth:
                    coordinator.ApplyCoauthSession(document, subRequest, subResponse);
                    break;
                default:
                    throw new InvalidOperationException($"{subRequest.Type} is not a coordination subrequest.");
            }

            var next = document.CaptureCoordination(current, coordinator.Capture());
            next = next with { Coordination = next.Coordination with { Generation = checked(current.Coordination.Generation + 1) } };
            return new StateTransition<bool>(next, true);
        }, cancellationToken).AsTask();

    private async Task<CellExecution> ExecuteCellAsync(IWopiWritableFile file, Guid resourceId, DocumentPartitionKind kind, FsshttpbCellRequest request, IReadOnlyDictionary<string, string> attributes, CellBridgeActor actor, CancellationToken cancellationToken)
    {
        var before = await _storage.State.FindByResourceIdAsync(resourceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"cellbridge lost the document for WOPI file '{file.Identifier}'.");

        var execution = await _documents.ExecuteAsync(resourceId, kind, request, attributes, actor, cancellationToken).ConfigureAwait(false);
        if (execution.LockError is { } lockError)
        {
            LogLockError(_logger, file.Identifier, lockError);
        }

        if (execution.State.ContentVersion != before.ContentVersion)
        {
            await MirrorContentAsync(file, execution.State, cancellationToken).ConfigureAwait(false);
        }

        return execution;
    }

    // cellbridge has already published the revision durably in its own store; this copies the
    // materialized Office package out to the WOPI file so GetFile and non-Cobalt clients see it.
    private async Task MirrorContentAsync(IWopiWritableFile file, DocumentState state, CancellationToken cancellationToken)
    {
        var gate = _writeLocks.GetOrAdd(file.Identifier, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var source = await _storage.Content.OpenReadAsync(state.Content, cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                var target = await file.OpenWriteAsync(cancellationToken).ConfigureAwait(false);
                await using (target.ConfigureAwait(false))
                {
                    await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                }
            }

            LogContentFlushed(_logger, file.Identifier, state.ContentVersion);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Guid> GetOrImportAsync(IWopiWritableFile file, CellBridgeActor owner, CancellationToken cancellationToken)
    {
        var lazy = _resources.GetOrAdd(
            file.Identifier,
            _ => new Lazy<Task<Guid>>(() => ImportAsync(file, owner, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A faulted Lazy<Task<>> would otherwise be cached forever; remove exactly this entry so
            // the next call retries.
            _resources.TryRemove(new KeyValuePair<string, Lazy<Task<Guid>>>(file.Identifier, lazy));
            LogImportFailed(_logger, ex, file.Identifier);
            throw;
        }
    }

    private async Task<Guid> ImportAsync(IWopiWritableFile file, CellBridgeActor owner, CancellationToken cancellationToken)
    {
        byte[] bytes = [];
        if (file.Exists)
        {
            var stream = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                bytes = buffer.ToArray();
            }
        }

        var path = DocumentPath(file);
        var state = await _documents.ImportAsync(path, bytes, owner.Identity, s_importer, cancellationToken).ConfigureAwait(false)
            ?? await _storage.State.FindByPathKeyAsync(StorageIds.PathKey(DocumentStore.NormalizeUrl(path)), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"cellbridge did not register a document for WOPI file '{file.Identifier}'.");

        LogDocumentImported(_logger, file.Identifier, state.ResourceId);
        return state.ResourceId;
    }

    // cellbridge validates Office packages by the extension in the document path, so the WOPI
    // file's extension has to survive into cellbridge's URL space.
    private static string DocumentPath(IWopiFile file)
    {
        var name = "/wopi/" + Uri.EscapeDataString(file.Identifier);
        return string.IsNullOrEmpty(file.Extension) ? name : name + "." + file.Extension;
    }

    private static CellBridgeActor ActorFor(ClaimsPrincipal? principal)
    {
        var id = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = principal?.FindFirst(ClaimTypes.Name)?.Value;
        var subject = string.IsNullOrEmpty(id) ? "wopi:anonymous" : "wopi:" + id;
        var login = string.IsNullOrEmpty(name) ? subject : name;
        return new CellBridgeActor(new SubjectIdentity(subject, login, login));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        foreach (var gate in _writeLocks.Values)
        {
            gate.Dispose();
        }

        _writeLocks.Clear();
        _resources.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, typeof(CellBridgeProcessor));
    }
}
