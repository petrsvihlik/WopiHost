using System.Buffers.Binary;
using CellBridge.FssHttpB;

namespace WopiHost.CellBridge;

/// <summary>
/// Sniffs the framing of a WOPI <c>COBALT</c> request body. The endpoint contract carries bytes
/// only, so the body itself has to say whether it is a raw MS-FSSHTTPB cell request or a SOAP
/// cell storage request.
/// </summary>
internal static class WopiCobaltFraming
{
    internal enum Kind
    {
        Unknown,
        Fsshttpb,
        Soap,
    }

    // MS-FSSHTTPB 2.2.2.1.1: ProtocolVersion (2), MinimumVersion (2), then the 8-byte signature.
    private const int SignatureOffset = 4;

    internal static Kind Detect(ReadOnlySpan<byte> body)
    {
        if (body.Length >= SignatureOffset + sizeof(ulong) &&
            BinaryPrimitives.ReadUInt64LittleEndian(body[SignatureOffset..]) == FsshttpbCellRequest.RequestSignature)
        {
            return Kind.Fsshttpb;
        }

        var text = body.StartsWith("\xEF\xBB\xBF"u8) ? body[3..] : body;
        text = text.TrimStart(" \t\r\n"u8);
        return text.Length > 0 && text[0] == (byte)'<' ? Kind.Soap : Kind.Unknown;
    }
}
