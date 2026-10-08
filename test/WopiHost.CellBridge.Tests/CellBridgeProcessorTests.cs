using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using CellBridge.FssHttpB;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.CellBridge.Tests;

/// <summary>
/// Drives raw MS-FSSHTTPB cell requests and SOAP cell storage requests through
/// <see cref="CellBridgeProcessor"/> end to end: the WOPI file is imported into cellbridge, queried,
/// locked and saved back. MTOM framing and the exact bytes Office Online Server posts over WOPI are
/// not covered — see the project README.
/// </summary>
public class CellBridgeProcessorTests
{
    private static readonly byte[] s_content = Encoding.UTF8.GetBytes("Hello from WOPI");

    private static CellBridgeProcessor CreateProcessor() =>
        new(NullLogger<CellBridgeProcessor>.Instance,
            Options.Create(new CellBridgeProcessorOptions { SerializationProfile = FsshttpbSerializationProfile.Current }));

    private static IWopiWritableFile CreateFile(string identifier = "file-1", byte[]? content = null, string extension = "txt")
    {
        var file = A.Fake<IWopiWritableFile>();
        A.CallTo(() => file.Identifier).Returns(identifier);
        A.CallTo(() => file.Exists).Returns(true);
        A.CallTo(() => file.Extension).Returns(extension);
        A.CallTo(() => file.OpenReadAsync(A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(OpenRead(content ?? s_content)));
        return file;
    }

    // The processor owns and disposes the stream it reads.
    private static Stream OpenRead(byte[] content) => new MemoryStream(content, writable: false);

    private static ClaimsPrincipal CreatePrincipal() => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, "user-42"),
        new Claim(ClaimTypes.Name, "Ada Lovelace"),
    ], "test"));

    private static byte[] CellRequest(params FsshttpbCellSubRequest[] subRequests)
    {
        var request = new FsshttpbCellRequest();
        request.SubRequests.AddRange(subRequests);
        return request.ToByteArray();
    }

    [Fact]
    public void Ctor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CellBridgeProcessor(null!, Options.Create(new CellBridgeProcessorOptions())));
    }

    [Fact]
    public void Ctor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CellBridgeProcessor(NullLogger<CellBridgeProcessor>.Instance, null!));
    }

    [Fact]
    public async Task ProcessCobalt_NullFile_ThrowsArgumentNullException()
    {
        using var processor = CreateProcessor();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => processor.ProcessCobalt(null!, CreatePrincipal(), [], CancellationToken.None));
    }

    [Fact]
    public async Task ProcessCobalt_NullContent_ThrowsArgumentNullException()
    {
        using var processor = CreateProcessor();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => processor.ProcessCobalt(CreateFile(), CreatePrincipal(), null!, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessCobalt_AfterDispose_Throws()
    {
        using var processor = CreateProcessor();
        processor.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => processor.ProcessCobalt(CreateFile(), CreatePrincipal(), [], CancellationToken.None));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        using var processor = CreateProcessor();
        processor.Dispose();
    }

    [Fact]
    public async Task ProcessCobalt_UnknownFraming_ThrowsNotSupported()
    {
        using var processor = CreateProcessor();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => processor.ProcessCobalt(CreateFile(), CreatePrincipal(), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13], CancellationToken.None));
    }

    [Fact]
    public void Detect_RecognizesSoapAndBinaryFraming()
    {
        Assert.Equal(WopiCobaltFraming.Kind.Soap, WopiCobaltFraming.Detect("  <soap:Envelope/>"u8));
        Assert.Equal(WopiCobaltFraming.Kind.Fsshttpb, WopiCobaltFraming.Detect(CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryAccess) { RequestId = 1, Data = new QueryAccessSubRequestData() })));
        Assert.Equal(WopiCobaltFraming.Kind.Unknown, WopiCobaltFraming.Detect("PK\u0003\u0004"u8));
        Assert.Equal(WopiCobaltFraming.Kind.Unknown, WopiCobaltFraming.Detect([]));
    }

    [Fact]
    public async Task ProcessCobalt_QueryAccess_GrantsReadAndWrite()
    {
        using var processor = CreateProcessor();
        var body = CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryAccess) { RequestId = 7, Data = new QueryAccessSubRequestData() });

        var responseBytes = await processor.ProcessCobalt(CreateFile(), CreatePrincipal(), body, CancellationToken.None);

        var response = FsshttpbResponse.Deserialize(new BinaryReaderEx(responseBytes));
        Assert.False(response.Status);
        var sub = Assert.Single(response.SubResponses);
        Assert.Equal(7UL, sub.RequestId);
        Assert.Equal(RequestTypes.QueryAccess, sub.RequestType);
        Assert.False(sub.Status);
        var access = Assert.IsType<QueryAccessSubResponseData>(sub.Data);
        Assert.Null(access.ReadAccessError);
        Assert.Null(access.WriteAccessError);
    }

    [Fact]
    public async Task ProcessCobalt_QueryChanges_ReturnsImportedFileGraph()
    {
        using var processor = CreateProcessor();
        var file = CreateFile();
        var body = CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryChanges) { RequestId = 3, Data = new QueryChangesSubRequestData() });

        var responseBytes = await processor.ProcessCobalt(file, CreatePrincipal(), body, CancellationToken.None);

        var response = FsshttpbResponse.Deserialize(new BinaryReaderEx(responseBytes));
        var sub = Assert.Single(response.SubResponses);
        Assert.Equal(RequestTypes.QueryChanges, sub.RequestType);
        Assert.False(sub.Status);
        Assert.NotNull(response.DataElementPackage);
        Assert.NotEmpty(response.DataElementPackage.DataElements);
        // The import reads the WOPI file exactly once; queries never touch it again.
        A.CallTo(() => file.OpenReadAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => file.OpenWriteAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessCobalt_SoapWhoAmI_AnswersWithThePrincipal()
    {
        using var processor = CreateProcessor();
        const string envelope = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <ExecuteCellStorageRequest xmlns="http://schemas.microsoft.com/sharepoint/soap/">
                  <RequestVersion Version="2" MinorVersion="2" />
                  <RequestCollection CorrelationId="6B29FC40-CA47-1067-B31D-00DD010662DA">
                    <Request Url="https://wopi.example.test/wopi/file-1.txt" RequestToken="1">
                      <SubRequest Type="WhoAmI" SubRequestToken="1" />
                      <SubRequest Type="ServerTime" SubRequestToken="2" />
                      <SubRequest Type="GetVersions" SubRequestToken="3" />
                    </Request>
                  </RequestCollection>
                </ExecuteCellStorageRequest>
              </soap:Body>
            </soap:Envelope>
            """;

        var responseBytes = await processor.ProcessCobalt(CreateFile(), CreatePrincipal(), Encoding.UTF8.GetBytes(envelope), CancellationToken.None);

        var response = Encoding.UTF8.GetString(responseBytes);
        Assert.Contains("UserName=\"Ada Lovelace\"", response, StringComparison.Ordinal);
        Assert.Contains("UserLogin=\"Ada Lovelace\"", response, StringComparison.Ordinal);
        Assert.Contains("ServerTime=\"", response, StringComparison.Ordinal);
        // cellbridge keeps GetVersions inside its own HTTP endpoint; the adapter must say so rather than fake a success.
        Assert.Contains("SubRequestToken=\"3\" ErrorCode=\"NotSupported\"", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessCobalt_SameFileTwice_ReusesTheImportedDocument()
    {
        using var processor = CreateProcessor();
        var file = CreateFile();
        var body = CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryAccess) { RequestId = 1, Data = new QueryAccessSubRequestData() });

        await processor.ProcessCobalt(file, CreatePrincipal(), body, CancellationToken.None);
        await processor.ProcessCobalt(file, CreatePrincipal(), body, CancellationToken.None);

        A.CallTo(() => file.OpenReadAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessCobalt_BinaryRequest_WithUnknownTargetPartition_ThrowsNotSupported()
    {
        using var processor = CreateProcessor();
        var body = CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryAccess) { RequestId = 1, TargetPartitionId = Guid.NewGuid(), Data = new QueryAccessSubRequestData() });

        await Assert.ThrowsAsync<NotSupportedException>(
            () => processor.ProcessCobalt(CreateFile(), CreatePrincipal(), body, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessCobalt_PutChanges_WritesThePublishedRevisionToTheWopiFile()
    {
        using var processor = CreateProcessor();
        using var written = new MemoryStream();
        // cellbridge accepts a save only as a well-formed Office package for the document's extension.
        var file = CreateFile(content: MinimalDocx("before"), extension: "docx");
        A.CallTo(() => file.OpenWriteAsync(A<CancellationToken>._)).Returns(Task.FromResult<Stream>(written));
        var principal = CreatePrincipal();
        var newContent = MinimalDocx("after");

        // A client learns the document's cell identity and storage index from QueryChanges before it can
        // save; the storage manifest only comes back when asked for, as Office does on open.
        var queryBytes = await processor.ProcessCobalt(file, principal,
            CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryChanges) { RequestId = 1, Data = new QueryChangesSubRequestData { IncludeStorageManifest = true, IncludeCellChanges = true } }), CancellationToken.None);
        var query = FsshttpbResponse.Deserialize(new BinaryReaderEx(queryBytes));

        var saveBytes = await processor.ProcessCobalt(file, principal, PutChanges(query, requestId: 2, newContent), CancellationToken.None);

        var save = FsshttpbResponse.Deserialize(new BinaryReaderEx(saveBytes));
        var sub = Assert.Single(save.SubResponses);
        Assert.Equal(RequestTypes.PutChanges, sub.RequestType);
        Assert.False(sub.Status, sub.Error?.ErrorMessage);
        A.CallTo(() => file.OpenWriteAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.Equal(newContent, written.ToArray());
    }

    [Fact]
    public async Task ProcessCobalt_FailedImport_IsRetriedOnTheNextCall()
    {
        using var processor = CreateProcessor();
        var file = CreateFile();
        A.CallTo(() => file.OpenReadAsync(A<CancellationToken>._))
            .Throws(new IOException("storage offline")).Once()
            .Then.ReturnsLazily(() => Task.FromResult(OpenRead(s_content)));
        var body = CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryAccess) { RequestId = 1, Data = new QueryAccessSubRequestData() });

        await Assert.ThrowsAsync<IOException>(() => processor.ProcessCobalt(file, CreatePrincipal(), body, CancellationToken.None));
        var responseBytes = await processor.ProcessCobalt(file, CreatePrincipal(), body, CancellationToken.None);

        Assert.False(Assert.Single(FsshttpbResponse.Deserialize(new BinaryReaderEx(responseBytes)).SubResponses).Status);
        A.CallTo(() => file.OpenReadAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ProcessCobalt_SoapCell_ExecutesTheEmbeddedBinaryRequest()
    {
        using var processor = CreateProcessor();
        var cell = CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryAccess) { RequestId = 5, Data = new QueryAccessSubRequestData() });
        var body = SoapEnvelope($"""
            <SubRequest Type="Cell" SubRequestToken="1">
              <SubRequestData BinaryDataSize="{cell.Length}">{Convert.ToBase64String(cell)}</SubRequestData>
            </SubRequest>
            """);

        var responseBytes = await processor.ProcessCobalt(CreateFile(), CreatePrincipal(), body, CancellationToken.None);

        var sub = SubResponse(responseBytes, 1);
        Assert.Equal("Success", (string?)sub.Attribute("ErrorCode"));
        var payload = Convert.FromBase64String(SubResponseData(sub).Value);
        var inner = Assert.Single(FsshttpbResponse.Deserialize(new BinaryReaderEx(payload)).SubResponses);
        Assert.Equal(5UL, inner.RequestId);
        Assert.IsType<QueryAccessSubResponseData>(inner.Data);
    }

    [Fact]
    public async Task ProcessCobalt_SoapCell_WithUnknownPartition_IsInvalidArgument()
    {
        using var processor = CreateProcessor();
        var cell = CellRequest(new FsshttpbCellSubRequest(RequestTypes.QueryAccess) { RequestId = 1, Data = new QueryAccessSubRequestData() });
        var body = SoapEnvelope($"""
            <SubRequest Type="Cell" SubRequestToken="1">
              <SubRequestData PartitionID="{Guid.NewGuid():D}" BinaryDataSize="{cell.Length}">{Convert.ToBase64String(cell)}</SubRequestData>
            </SubRequest>
            """);

        var responseBytes = await processor.ProcessCobalt(CreateFile(), CreatePrincipal(), body, CancellationToken.None);

        Assert.Equal("InvalidArgument", (string?)SubResponse(responseBytes, 1).Attribute("ErrorCode"));
    }

    [Fact]
    public async Task ProcessCobalt_SoapCell_WithUndecodablePayload_IsInvalidArgument()
    {
        using var processor = CreateProcessor();
        var body = SoapEnvelope("""
            <SubRequest Type="Cell" SubRequestToken="1">
              <SubRequestData BinaryDataSize="4">not base64</SubRequestData>
            </SubRequest>
            """);

        var responseBytes = await processor.ProcessCobalt(CreateFile(), CreatePrincipal(), body, CancellationToken.None);

        Assert.Equal("InvalidArgument", (string?)SubResponse(responseBytes, 1).Attribute("ErrorCode"));
    }

    [Fact]
    public async Task ProcessCobalt_SoapDependency_OnSuccess_IsNotExecutedAfterAFailure()
    {
        using var processor = CreateProcessor();
        var body = SoapEnvelope("""
            <SubRequest Type="GetVersions" SubRequestToken="1" />
            <SubRequest Type="WhoAmI" SubRequestToken="2" DependsOn="1" DependencyType="OnSuccess" />
            """);

        var responseBytes = await processor.ProcessCobalt(CreateFile(), CreatePrincipal(), body, CancellationToken.None);

        Assert.Equal("NotSupported", (string?)SubResponse(responseBytes, 1).Attribute("ErrorCode"));
        var dependent = SubResponse(responseBytes, 2);
        Assert.Equal("DependentOnlyOnSuccessRequestFailed", (string?)dependent.Attribute("ErrorCode"));
        Assert.Null(SubResponseData(dependent).Attribute("UserName"));
    }

    [Fact]
    public async Task ProcessCobalt_SoapSchemaLock_IsGrantedReportedAndReleased()
    {
        using var processor = CreateProcessor();
        var file = CreateFile();
        var principal = CreatePrincipal();
        var schemaLockId = Guid.NewGuid().ToString("D");
        var clientId = Guid.NewGuid().ToString("D");

        var granted = await processor.ProcessCobalt(file, principal, SoapEnvelope($"""
            <SubRequest Type="SchemaLock" SubRequestToken="1">
              <SubRequestData SchemaLockRequestType="GetLock" SchemaLockID="{schemaLockId}" ClientID="{clientId}" Timeout="3600" />
            </SubRequest>
            <SubRequest Type="LockStatus" SubRequestToken="2" />
            """), CancellationToken.None);

        var lockResponse = SubResponse(granted, 1);
        Assert.Equal("Success", (string?)lockResponse.Attribute("ErrorCode"));
        Assert.Equal("SchemaLock", (string?)SubResponseData(lockResponse).Attribute("LockType"));
        var status = SubResponseData(SubResponse(granted, 2));
        // MS-FSSHTTP LockStatus: 1 = schema lock held.
        Assert.Equal("1", (string?)status.Attribute("LockType"));
        Assert.NotNull(status.Attribute("LockID"));

        var released = await processor.ProcessCobalt(file, principal, SoapEnvelope($"""
            <SubRequest Type="SchemaLock" SubRequestToken="1">
              <SubRequestData SchemaLockRequestType="ReleaseLock" SchemaLockID="{schemaLockId}" ClientID="{clientId}" />
            </SubRequest>
            <SubRequest Type="LockStatus" SubRequestToken="2" />
            """), CancellationToken.None);

        Assert.Equal("Success", (string?)SubResponse(released, 1).Attribute("ErrorCode"));
        // 0 = no lock on the document.
        Assert.Equal("0", (string?)SubResponseData(SubResponse(released, 2)).Attribute("LockType"));
    }

    [Fact]
    public async Task ProcessCobalt_SoapExclusiveLock_BlocksASecondUserOnTheSameDocument()
    {
        using var processor = CreateProcessor();
        var file = CreateFile();
        var other = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-99"),
            new Claim(ClaimTypes.Name, "Grace Hopper"),
        ], "test"));

        var first = await processor.ProcessCobalt(file, CreatePrincipal(), ExclusiveLock(Guid.NewGuid()), CancellationToken.None);
        var second = await processor.ProcessCobalt(file, other, ExclusiveLock(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Success", (string?)SubResponse(first, 1).Attribute("ErrorCode"));
        Assert.Equal("FileAlreadyLockedOnServer", (string?)SubResponse(second, 1).Attribute("ErrorCode"));
    }

    [Fact]
    public async Task ProcessCobalt_SoapJoinCoauthoring_ReportsASingleEditorAsAlone()
    {
        using var processor = CreateProcessor();
        var file = CreateFile();
        var principal = CreatePrincipal();

        var joined = await processor.ProcessCobalt(file, principal, SoapEnvelope($"""
            <SubRequest Type="Coauth" SubRequestToken="1">
              <SubRequestData CoauthRequestType="JoinCoauthoring" SchemaLockID="{Guid.NewGuid():D}" ClientID="{Guid.NewGuid():D}" Timeout="3600" />
            </SubRequest>
            """), CancellationToken.None);

        var session = SubResponse(joined, 1);
        Assert.Equal("Success", (string?)session.Attribute("ErrorCode"));
        Assert.Equal("Alone", (string?)SubResponseData(session).Attribute("CoauthStatus"));
        var transitionId = (string?)SubResponseData(session).Attribute("TransitionID");
        Assert.NotNull(transitionId);

        var alone = await processor.ProcessCobalt(file, principal, SoapEnvelope($"""
            <SubRequest Type="AmIAlone" SubRequestToken="1">
              <SubRequestData TransitionID="{transitionId}" />
            </SubRequest>
            """), CancellationToken.None);

        Assert.Equal("True", (string?)SubResponseData(SubResponse(alone, 1)).Attribute("AmIAlone"));
    }

    // Mirrors what a client does with a QueryChanges response: reuse the server's cell identity and
    // current storage index, and propose a new storage index over a freshly built content graph.
    private static byte[] PutChanges(FsshttpbResponse query, ulong requestId, byte[] content)
    {
        var queried = Assert.IsType<QueryChangesSubResponseData>(Assert.Single(query.SubResponses).Data);
        var cell = PartitionGraphSnapshot.Create(query.DataElementPackage!.DataElements, queried.StorageIndexExtendedGuid).FileCell;

        static ExGuid Fresh() => new(1, Guid.NewGuid());
        var identity = new StorageManifestBuilder.StableIdentity(Fresh(), Fresh(), Fresh(), Fresh(), Fresh(), Fresh(), Fresh(), cell, Guid.NewGuid());
        var graph = FileContentPartitionBuilder.BuildQueryChangesResponse(requestId, content, identity, queried.CellKnowledgeTo + 1);
        var proposed = ((QueryChangesSubResponseData)graph.SubResponses.Single().Data!).StorageIndexExtendedGuid;

        var request = new FsshttpbCellRequest { DataElementPackage = graph.DataElementPackage };
        request.SubRequests.Add(new FsshttpbCellSubRequest(RequestTypes.PutChanges)
        {
            RequestId = requestId,
            Data = new PutChangesSubRequestData { StorageIndex = proposed, ExpectedStorageIndex = queried.StorageIndexExtendedGuid },
        });
        return request.ToByteArray();
    }

    // The smallest OPC package cellbridge's save validation accepts for a .docx: content types plus the
    // main document part.
    private static byte[] MinimalDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open(), new UTF8Encoding(false)))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="xml" ContentType="application/xml"/>
                      <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                    </Types>
                    """);
            }

            using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false)))
            {
                writer.Write($"""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                      <w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body>
                    </w:document>
                    """);
            }
        }

        return stream.ToArray();
    }

    private static byte[] ExclusiveLock(Guid lockId) => SoapEnvelope($"""
        <SubRequest Type="ExclusiveLock" SubRequestToken="1">
          <SubRequestData ExclusiveLockRequestType="GetLock" ExclusiveLockID="{lockId:D}" ClientID="{Guid.NewGuid():D}" Timeout="3600" />
        </SubRequest>
        """);

    private static byte[] SoapEnvelope(string subRequests) => Encoding.UTF8.GetBytes($"""
        <?xml version="1.0" encoding="utf-8"?>
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <ExecuteCellStorageRequest xmlns="http://schemas.microsoft.com/sharepoint/soap/">
              <RequestVersion Version="2" MinorVersion="2" />
              <RequestCollection CorrelationId="6B29FC40-CA47-1067-B31D-00DD010662DA">
                <Request Url="https://wopi.example.test/wopi/file-1.txt" RequestToken="1">
                  {subRequests}
                </Request>
              </RequestCollection>
            </ExecuteCellStorageRequest>
          </soap:Body>
        </soap:Envelope>
        """);

    private static XElement SubResponse(byte[] soapResponse, int subRequestToken) =>
        XDocument.Parse(Encoding.UTF8.GetString(soapResponse)).Descendants()
            .Single(e => e.Name.LocalName == "SubResponse"
                         && (string?)e.Attribute("SubRequestToken") == subRequestToken.ToString(CultureInfo.InvariantCulture));

    private static XElement SubResponseData(XElement subResponse) =>
        subResponse.Elements().Single(e => e.Name.LocalName == "SubResponseData");
}
