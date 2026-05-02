using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Resolves the owning user name for a file on Linux via libc's
/// <c>statx</c> (file UID) and <c>getpwuid_r</c> (UID → name).
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
    private const int ERANGE = 34;
    private const int InitialPasswdBufferSize = 1024;
    private const int MaxPasswdBufferSize = 64 * 1024;

    public static string GetOwnerName(string filePath)
    {
        if (Statx(AT_FDCWD, filePath, 0, STATX_UID, out var stx) != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new IOException(
                FormattableString.Invariant($"statx failed for '{filePath}' (errno {errno})."));
        }

        return ResolveUserName(stx.stx_uid)
            ?? stx.stx_uid.ToString(CultureInfo.InvariantCulture);
    }

    private static string? ResolveUserName(uint uid)
    {
        var bufferSize = InitialPasswdBufferSize;
        while (true)
        {
            var passwd = default(Passwd);
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                var rc = GetPwUid_R(uid, ref passwd, buffer, (nuint)bufferSize, out var resultPtr);
                if (rc == 0)
                {
                    return resultPtr == IntPtr.Zero
                        ? null
                        : Marshal.PtrToStringUTF8(passwd.pw_name);
                }

                if (rc == ERANGE && bufferSize < MaxPasswdBufferSize)
                {
                    bufferSize *= 2;
                    continue;
                }

                throw new IOException(
                    FormattableString.Invariant($"getpwuid_r failed for uid {uid} (errno {rc})."));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
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

    [StructLayout(LayoutKind.Sequential)]
    private struct Passwd
    {
        public IntPtr pw_name;
        public IntPtr pw_passwd;
        public uint pw_uid;
        public uint pw_gid;
        public IntPtr pw_gecos;
        public IntPtr pw_dir;
        public IntPtr pw_shell;
    }

#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
    [LibraryImport("libc", EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Statx(
        int dirfd,
        string pathname,
        int flags,
        uint mask,
        out StatxBuf statxbuf);

    [LibraryImport("libc", EntryPoint = "getpwuid_r", SetLastError = false)]
    private static partial int GetPwUid_R(
        uint uid,
        ref Passwd pwd,
        IntPtr buf,
        nuint buflen,
        out IntPtr result);
#pragma warning restore CA5392
}
