using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CellBridge.AspNetCore;
using CellBridge.FssHttp;
using CellBridge.FssHttpB;
using CellBridge.Storage;
using CellBridge.Storage.Abstractions;
using CellBridge.Storage.InMemory;
using Microsoft.AspNetCore.Http;
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
/// into cellbridge on first use under a resource id derived from its WOPI identifier, and every save
/// cellbridge accepts is written back through <see cref="IWopiWritableFile.OpenWriteAsync"/>.
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
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly StorageProvider _storage;
    private readonly CellBridgeDocumentService _documents;
    private readonly CellBridgeRequestProcessor _requests;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _writeLocks = new();
    private readonly ConcurrentDictionary<Guid, uint> _writtenVersions = new();
    private int _disposed;

    public CellBridgeProcessor(ILoggerFactory loggerFactory, IOptions<CellBridgeProcessorOptions> options, IHttpContextAccessor? httpContextAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<CellBridgeProcessor>();
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpContextAccessor = httpContextAccessor;
        _storage = new StorageProvider(new InMemoryStateStore(), new InMemoryContentStore());
        _documents = new CellBridgeDocumentService(_storage, authorizationPolicy: new WopiCellBridgeAuthorizationPolicy());
        _requests = new CellBridgeRequestProcessor(_documents, loggerFactory.CreateLogger<CellBridgeRequestProcessor>());
    }

    /// <inheritdoc/>
    public async Task<byte[]> ProcessCobalt(IWopiWritableFile file, ClaimsPrincipal principal, byte[] newContent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(newContent);

        var resourceId = ResourceIdFor(file);
        var actor = ActorFor(principal, resourceId);
        await EnsureImportedAsync(file, resourceId, actor.Identity, cancellationToken).ConfigureAwait(false);

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

        var execution = await WriteBackAsync(file, async () =>
        {
            var result = await _documents.ExecuteAsync(resourceId, kind, request, s_noAttributes, actor, cancellationToken).ConfigureAwait(false);
            return (result, result.AcceptedSaves);
        }, cancellationToken).ConfigureAwait(false);

        if (execution.LockError is { } lockError)
        {
            LogLockError(_logger, file.Identifier, lockError);
        }

        return execution.Response.ToByteArray(_options.SerializationProfile);
    }

    private async Task<byte[]> ExecuteSoapAsync(IWopiWritableFile file, Guid resourceId, CellBridgeActor actor, byte[] body, CancellationToken cancellationToken)
    {
        var request = CellStorageRequestParser.Parse(Encoding.UTF8.GetString(body));

        // The WOPI route has already chosen the file, so whatever URL the client put in the envelope is
        // only echoed back; cellbridge resolves every request to this document by its resource id.
        var clientUrls = request.Requests.ToDictionary(r => r.RequestToken, r => r.Url);
        foreach (var fileRequest in request.Requests)
        {
            fileRequest.UseResourceId = true;
            fileRequest.ResourceId = resourceId.ToString("D");
        }

        var execution = await WriteBackAsync(file, async () =>
        {
            var result = await _requests.ExecuteAsync(request, PublicOrigin(), actor, cancellationToken).ConfigureAwait(false);
            return (result, result.AcceptedSaves);
        }, cancellationToken).ConfigureAwait(false);

        foreach (var fileResponse in execution.Response.Responses)
        {
            if (fileResponse.RequestToken is { } token && clientUrls.TryGetValue(token, out var url))
            {
                fileResponse.Url = url;
            }
        }

        return Encoding.UTF8.GetBytes(execution.Response.ToSoapEnvelope());
    }

    // cellbridge has already published accepted saves durably in its own store; this copies the newest
    // one out to the WOPI file so GetFile and non-Cobalt clients see it. Saves accepted before a later
    // subrequest failed are still written before the failure propagates.
    private async Task<T> WriteBackAsync<T>(IWopiWritableFile file, Func<Task<(T Result, ImmutableArray<AcceptedSave> Saves)>> execute, CancellationToken cancellationToken)
    {
        (T Result, ImmutableArray<AcceptedSave> Saves) execution;
        try
        {
            execution = await execute().ConfigureAwait(false);
        }
        catch (AcceptedSaveException ex)
        {
            await WriteNewestAsync(file, ex.AcceptedSaves, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await WriteNewestAsync(file, execution.Saves, cancellationToken).ConfigureAwait(false);
        return execution.Result;
    }

    private async Task WriteNewestAsync(IWopiWritableFile file, ImmutableArray<AcceptedSave> saves, CancellationToken cancellationToken)
    {
        if (saves.Where(s => !s.IsReplay).MaxBy(s => s.ContentVersion) is not { } save)
        {
            return;
        }

        var gate = _writeLocks.GetOrAdd(save.ResourceId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Concurrent requests can finish out of order; an older revision must not overwrite a newer one.
            if (_writtenVersions.TryGetValue(save.ResourceId, out var written) && written >= save.ContentVersion)
            {
                return;
            }

            var source = await _storage.Content.OpenReadAsync(save.Content, cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                var target = await file.OpenWriteAsync(cancellationToken).ConfigureAwait(false);
                await using (target.ConfigureAwait(false))
                {
                    await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                }
            }

            _writtenVersions[save.ResourceId] = save.ContentVersion;
            LogContentFlushed(_logger, file.Identifier, save.ContentVersion);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureImportedAsync(IWopiWritableFile file, Guid resourceId, SubjectIdentity owner, CancellationToken cancellationToken)
    {
        if (await _storage.State.FindByResourceIdAsync(resourceId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return;
        }

        try
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

            // A null result means a concurrent request imported the same file first.
            if (await _documents.ImportAsync(resourceId, DocumentPath(file), bytes, owner, s_importer, cancellationToken).ConfigureAwait(false) is not null)
            {
                LogDocumentImported(_logger, file.Identifier, resourceId);
            }
        }
        catch (Exception ex)
        {
            LogImportFailed(_logger, ex, file.Identifier);
            throw;
        }
    }

    // Deterministic, so a durable cellbridge state store would find the same document after a restart.
    private static Guid ResourceIdFor(IWopiResource file) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(file.Identifier)).AsSpan(0, 16));

    // cellbridge validates Office packages by the extension in the document path, so the WOPI
    // file's extension has to survive into cellbridge's URL space.
    private static string DocumentPath(IWopiFile file)
    {
        var name = "/wopi/" + Uri.EscapeDataString(file.Identifier);
        return string.IsNullOrEmpty(file.Extension) ? name : name + "." + file.Extension;
    }

    // MS-FSSHTTP requires the SOAP WebUrl to be same-origin with the request.
    private string PublicOrigin()
    {
        if (!string.IsNullOrEmpty(_options.WebOrigin))
        {
            return _options.WebOrigin;
        }

        var request = _httpContextAccessor?.HttpContext?.Request
            ?? throw new InvalidOperationException($"SOAP framing needs the request origin: set {CellBridgeProcessorOptions.SectionName}:{nameof(CellBridgeProcessorOptions.WebOrigin)}.");
        return request.Scheme + "://" + request.Host.ToUriComponent() + "/";
    }

    // The COBALT endpoint only runs after the access token's update permission was checked, so the
    // ceiling grants read/write — but only on the file the WOPI route resolved.
    private static CellBridgeActor ActorFor(ClaimsPrincipal? principal, Guid resourceId)
    {
        var id = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = principal?.FindFirst(ClaimTypes.Name)?.Value;
        var subject = string.IsNullOrEmpty(id) ? "wopi:anonymous" : "wopi:" + id;
        var login = string.IsNullOrEmpty(name) ? subject : name;
        return new CellBridgeActor(new SubjectIdentity(subject, login, login))
        {
            AccessLimit = new DocumentAccessLimit(resourceId, DocumentAccess.Read | DocumentAccess.Write),
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        foreach (var gate in _writeLocks.Values)
        {
            gate.Dispose();
        }

        _writeLocks.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, typeof(CellBridgeProcessor));
    }
}
