using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AgentControl;

public readonly record struct FileFacts(long LinkCount, bool IsRegularFile);

/// <summary>
/// Hard-link count and file type of a path (st_nlink / S_IFREG). .NET has no portable API for
/// either, so ask the OS directly. An unsupported platform throws, and the guard then blocks.
/// </summary>
public static partial class NativeLinks
{
    private const int FileTypeMask = 0xF000; // S_IFMT
    private const int RegularFile = 0x8000;  // S_IFREG

    public static FileFacts Inspect(string path)
    {
        if (OperatingSystem.IsLinux())
            return Linux(path);
        if (OperatingSystem.IsMacOS())
            return Mac(path);
        if (OperatingSystem.IsWindows())
            return Windows(path);
        throw new PlatformNotSupportedException("file facts are not available on this platform");
    }

    // struct statx is identical on every Linux architecture:
    // stx_mask u32 @0, stx_nlink u32 @16, stx_mode u16 @28.
    private const int AtFdCwd = -100;
    private const int AtSymlinkNoFollow = 0x100;
    private const uint StatxType = 0x1, StatxMode = 0x2, StatxNlink = 0x4;

    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Statx(int dirfd, string pathname, int flags, uint mask, byte[] buffer);

    private static FileFacts Linux(string path)
    {
        var buffer = new byte[256];
        const uint wanted = StatxType | StatxMode | StatxNlink;
        if (Statx(AtFdCwd, path, AtSymlinkNoFollow, wanted, buffer) != 0)
            throw new IOException("statx failed");
        if ((BitConverter.ToUInt32(buffer, 0) & wanted) != wanted)
            throw new IOException("statx did not report type and link count");
        return new FileFacts(BitConverter.ToUInt32(buffer, 16), (BitConverter.ToUInt16(buffer, 28) & FileTypeMask) == RegularFile);
    }

    // macOS struct stat (64-bit inodes): st_dev int32 @0, st_mode uint16 @4, st_nlink uint16 @6.
    [LibraryImport("libc", EntryPoint = "lstat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int LstatArm64(string path, byte[] buffer);

    [LibraryImport("libc", EntryPoint = "lstat$INODE64", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int LstatX64(string path, byte[] buffer);

    private static FileFacts Mac(string path)
    {
        var buffer = new byte[256];
        var rc = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? LstatArm64(path, buffer) : LstatX64(path, buffer);
        if (rc != 0)
            throw new IOException("lstat failed");
        return new FileFacts(BitConverter.ToUInt16(buffer, 6), (BitConverter.ToUInt16(buffer, 4) & FileTypeMask) == RegularFile);
    }

    // BY_HANDLE_FILE_INFORMATION (52 bytes): dwFileAttributes u32 @0, nNumberOfLinks u32 @40.
    private const uint AttributeDirectory = 0x10, AttributeDevice = 0x40, AttributeReparsePoint = 0x400;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandle(SafeFileHandle handle, byte[] information);

    private static FileFacts Windows(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var information = new byte[52];
        if (!GetFileInformationByHandle(handle, information))
            throw new IOException("GetFileInformationByHandle failed");
        var attributes = BitConverter.ToUInt32(information, 0);
        var regular = (attributes & (AttributeDirectory | AttributeDevice | AttributeReparsePoint)) == 0;
        return new FileFacts(BitConverter.ToUInt32(information, 40), regular);
    }
}
