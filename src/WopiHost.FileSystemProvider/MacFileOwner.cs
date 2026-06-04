using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Resolves the owning user name for a file on macOS via libc's <c>stat</c>
/// (file UID) and, through <see cref="UnixUserResolver"/>, <c>getpwuid_r</c>
/// (UID → name).
/// </summary>
/// <remarks>
/// macOS has no <c>statx</c>, so we P/Invoke <c>stat</c> directly. The wrinkle is the
/// 64-bit-inode ABI transition: on x86_64 the modern struct is exported under the
/// <c>stat$INODE64</c> symbol (the bare <c>stat</c> is the deprecated 32-bit-inode
/// variant), whereas arm64 — which never shipped a 32-bit-inode ABI — exports it as
/// plain <c>stat</c>. We pick the right entry point by process architecture
/// (Rosetta reports <see cref="Architecture.X64"/> and uses the x86_64 ABI, so the
/// <c>$INODE64</c> symbol is still correct there). Both 64-bit ABIs share the same
/// <c>struct stat</c> layout, so a single struct definition serves both — <c>st_uid</c>
/// sits at offset 16 and the record is 144 bytes.
/// </remarks>
[SupportedOSPlatform("macos")]
[ExcludeFromCodeCoverage(Justification = "Native macOS stat P/Invoke; cannot execute on the Linux coverage-collection runner — verified by the macOS leg of the filesystem-owner-cross-platform CI job.")]
internal static partial class MacFileOwner
{
    public static string GetOwnerName(string filePath)
    {
        var rc = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? StatX64(filePath, out var stat)
            : StatArm64(filePath, out stat);

        if (rc != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new IOException(
                FormattableString.Invariant($"stat failed for '{filePath}' (errno {errno})."));
        }

        return UnixUserResolver.ResolveUserNameOrUid(stat.st_uid);
    }

    // Leading fields of the 64-bit-inode `struct stat`; Size pads to the full 144-byte
    // record so the kernel has room to write the trailing fields we don't read.
    // st_dev(4) st_mode(2) st_nlink(2) st_ino(8) -> st_uid at offset 16.
    [StructLayout(LayoutKind.Sequential, Size = 144)]
    private struct StatBuf
    {
        public int st_dev;
        public ushort st_mode;
        public ushort st_nlink;
        public ulong st_ino;
        public uint st_uid;
        public uint st_gid;
    }

    // x86_64 (and Rosetta): the 64-bit-inode struct lives behind the $INODE64 symbol.
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("libc", EntryPoint = "stat$INODE64", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int StatX64(string pathname, out StatBuf statbuf);

    // arm64: plain `stat` is already the 64-bit-inode entry point.
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("libc", EntryPoint = "stat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int StatArm64(string pathname, out StatBuf statbuf);
}
