using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Resolves the owning user name for a file on Linux via libc's
/// <c>statx</c> (file UID) and, through <see cref="UnixUserResolver"/>,
/// <c>getpwuid_r</c> (UID → name).
/// </summary>
/// <remarks>
/// <c>statx</c> is used instead of <c>stat</c> because its struct layout is
/// kernel-defined and stable across architectures; the historical <c>stat</c>
/// struct differs between x86_64, arm64, and 32-bit ABIs which makes direct
/// P/Invoke fragile. Available on glibc ≥ 2.28 and musl ≥ 1.2.
/// </remarks>
[SupportedOSPlatform("linux")]
internal static partial class LinuxFileOwner
{
    private const int AT_FDCWD = -100;
    private const uint STATX_UID = 0x00000008;

    public static string GetOwnerName(string filePath)
    {
        if (Statx(AT_FDCWD, filePath, 0, STATX_UID, out var stx) != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new IOException(
                FormattableString.Invariant($"statx failed for '{filePath}' (errno {errno})."));
        }

        return UnixUserResolver.ResolveUserNameOrUid(stx.stx_uid);
    }

    // Only the leading fields are read; allocate the full kernel-defined
    // 256-byte size so statx has room to write the rest of the record.
    [StructLayout(LayoutKind.Sequential, Size = 256)]
    private struct StatxBuf
    {
        public uint stx_mask;
        public uint stx_blksize;
        public ulong stx_attributes;
        public uint stx_nlink;
        public uint stx_uid;
        public uint stx_gid;
    }

    // CA5392 wants explicit DLL search paths to limit the loader's search to safe directories
    // and prevent DLL hijacking. The attribute is honored on Windows only — on Linux the dynamic
    // loader uses LD_LIBRARY_PATH + system paths and ignores the attribute entirely. This whole
    // class is [SupportedOSPlatform("linux")] so the attribute is effectively a no-op for us, but
    // it satisfies the analyzer and documents intent.
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("libc", EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Statx(
        int dirfd,
        string pathname,
        int flags,
        uint mask,
        out StatxBuf statxbuf);
}
