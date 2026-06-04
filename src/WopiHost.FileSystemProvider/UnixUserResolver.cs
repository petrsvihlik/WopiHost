using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Resolves a Unix user id (UID) to its login name via libc's <c>getpwuid_r</c>.
/// Shared by the Linux and macOS file-owner helpers, which differ only in how they
/// obtain the file's UID (<c>statx</c> on Linux, <c>stat</c> on macOS) — the UID → name
/// step is identical POSIX on both.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static partial class UnixUserResolver
{
    private const int ERANGE = 34;
    private const int InitialPasswdBufferSize = 1024;
    private const int MaxPasswdBufferSize = 64 * 1024;

    /// <summary>
    /// Resolves <paramref name="uid"/> to a login name, falling back to the numeric UID
    /// (as an invariant string) when no matching passwd entry exists.
    /// </summary>
    public static string ResolveUserNameOrUid(uint uid)
        => ResolveUserName(uid) ?? uid.ToString(CultureInfo.InvariantCulture);

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

    // getpwuid_r writes the platform's full `struct passwd` here, then points `result` at it.
    // We only read pw_name — the FIRST field on both the glibc and the BSD/macOS layouts — so
    // we declare just that field and pad the record with an explicit Size large enough for the
    // largest platform. This sizing is load-bearing: glibc's struct passwd is 56 bytes, but
    // macOS/BSD's is larger (extra pw_change/pw_class/pw_expire members). Declaring only the
    // 7 glibc fields (as before) under-sizes the buffer on macOS, so getpwuid_r writes past the
    // end and corrupts memory — a SIGSEGV (exit 139) on arm64 macOS. 128 bytes comfortably
    // covers every supported Unix layout; over-allocating is harmless because getpwuid_r writes
    // only sizeof(struct passwd) for the running platform.
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    private struct Passwd
    {
        public IntPtr pw_name;
    }

    // CA5392 wants explicit DLL search paths to limit the loader's search to safe directories
    // and prevent DLL hijacking. The attribute is honored on Windows only — on Linux/macOS the
    // dynamic loader uses its own search rules and ignores the attribute entirely. This whole
    // class is [SupportedOSPlatform] linux/macos so the attribute is effectively a no-op for us,
    // but it satisfies the analyzer and documents intent.
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("libc", EntryPoint = "getpwuid_r", SetLastError = false)]
    private static partial int GetPwUid_R(
        uint uid,
        ref Passwd pwd,
        IntPtr buf,
        nuint buflen,
        out IntPtr result);
}
