using System.Security.Claims;
using System.Text;
using CellBridge.FssHttpB;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.CellBridge.Tests;

/// <summary>
/// Drives raw MS-FSSHTTPB cell requests through <see cref="CellBridgeProcessor"/> end to end: the
/// WOPI file is imported into cellbridge, queried, and saved back. The SOAP/MTOM framing and the
/// exact bytes Office Online Server posts over WOPI are not covered — see the project README.
/// </summary>
public class CellBridgeProcessorTests
{
    private static readonly byte[] s_content = Encoding.UTF8.GetBytes("Hello from WOPI");

    private static CellBridgeProcessor CreateProcessor() =>
        new(NullLogger<CellBridgeProcessor>.Instance,
            Options.Create(new CellBridgeProcessorOptions { SerializationProfile = FsshttpbSerializationProfile.Current }));

    private static IWopiWritableFile CreateFile(string identifier = "file-1", byte[]? content = null)
    {
        var file = A.Fake<IWopiWritableFile>();
        A.CallTo(() => file.Identifier).Returns(identifier);
        A.CallTo(() => file.Exists).Returns(true);
        A.CallTo(() => file.Extension).Returns("txt");
        A.CallTo(() => file.OpenReadAsync(A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult<Stream>(new MemoryStream(content ?? s_content, writable: false)));
        return file;
    }

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
        var processor = CreateProcessor();
        processor.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => processor.ProcessCobalt(CreateFile(), CreatePrincipal(), [], CancellationToken.None));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var processor = CreateProcessor();
        processor.Dispose();
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
}
