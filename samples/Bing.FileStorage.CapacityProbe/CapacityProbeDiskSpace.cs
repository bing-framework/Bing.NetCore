using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Bing.FileStorage.CapacityProbe;

internal sealed record CapacityDiskTarget(string DirectoryPath, string VolumeRoot);

internal sealed record CapacityDiskSnapshot(
    string DirectoryPath,
    string VolumeRoot,
    long RequiredBytes,
    long AvailableBytes)
{
    public bool HasEnoughSpace => AvailableBytes >= RequiredBytes;

    // 与预检查使用同一目录查询，避免监控和报告重新按路径盘符取样。
    public long ReadAvailableBytes() => CapacityProbeDiskSpace.QueryAvailableBytes(DirectoryPath);
}

internal static class CapacityProbeDiskSpace
{
    private const long LocalSafetyReserveBytes = 10L * 1024 * 1024 * 1024;

    public static CapacityDiskSnapshot Measure(
        string backendName,
        string rootDirectory,
        string tempDirectory,
        long fileLength,
        int partSize,
        int maxActualParallelism,
        Func<CapacityDiskTarget, long>? availableSpaceQuery = null,
        Func<string, string>? volumeRootResolver = null)
    {
        if (string.IsNullOrWhiteSpace(backendName)) throw new ArgumentException("Backend 名称不能为空。", nameof(backendName));
        if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Root 目录不能为空。", nameof(rootDirectory));
        if (string.IsNullOrWhiteSpace(tempDirectory)) throw new ArgumentException("Temp 目录不能为空。", nameof(tempDirectory));
        if (fileLength <= 0) throw new ArgumentOutOfRangeException(nameof(fileLength));
        if (partSize <= 0) throw new ArgumentOutOfRangeException(nameof(partSize));
        if (maxActualParallelism <= 0) throw new ArgumentOutOfRangeException(nameof(maxActualParallelism));

        var isLocal = backendName is "local" or "aliyundrive" or "pan115";
        if (!isLocal && backendName is not ("minio" or "qiniu" or "oss"))
            throw new ArgumentException("不支持的容量探针 Backend。", nameof(backendName));

        var directory = Path.GetFullPath(isLocal ? rootDirectory : tempDirectory);
        var volumeRoot = volumeRootResolver is null
            ? ResolveVolumeRoot(directory)
            : volumeRootResolver(directory);
        if (string.IsNullOrEmpty(volumeRoot))
            throw new IOException($"无法解析暂存目录所在卷：{directory}");

        var target = new CapacityDiskTarget(directory, volumeRoot);
        var requiredBytes = isLocal
            ? checked(fileLength * 3 + LocalSafetyReserveBytes)
            : checked((long)partSize * maxActualParallelism);
        var availableBytes = availableSpaceQuery is null ? QueryAvailableBytes(directory) : availableSpaceQuery(target);
        if (availableBytes < 0) throw new IOException("磁盘可用空间查询返回了负值。");

        return new CapacityDiskSnapshot(directory, volumeRoot, requiredBytes, availableBytes);
    }

    /// <summary>按实际目录查询当前调用者可用空间，操作系统负责跟随联接、符号链接及挂载点。</summary>
    internal static long QueryAvailableBytes(string directory)
    {
        EnsureDirectoryExists(directory);
        if (OperatingSystem.IsWindows())
        {
            // UNC 目录也必须带尾部分隔符；不能改为 DriveInfo，Windows 实现会截断到盘符。
            var queryPath = Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar;
            if (!GetDiskFreeSpaceEx(queryPath, out var available, out _, out _))
                throw NativeError("查询目录可用空间", directory);
            return checked((long)available);
        }
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            return new DriveInfo(directory).AvailableFreeSpace;
        throw new PlatformNotSupportedException("容量探针的文件系统查询仅支持 Windows、Linux 和 macOS。");
    }

    private static string ResolveVolumeRoot(string directory)
    {
        EnsureDirectoryExists(directory);
        if (OperatingSystem.IsWindows())
        {
            // 此 API 跟随完整的目录联接链，并保留卷挂载文件夹；Path.GetPathRoot 不具备此语义。
            var buffer = new StringBuilder(32768);
            if (!GetVolumePathName(directory, buffer, buffer.Capacity))
                throw NativeError("解析目录所在卷", directory);
            return buffer.ToString();
        }
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("容量探针的文件系统查询仅支持 Windows、Linux 和 macOS。");

        // realpath 同时解析祖先目录中的链接；从操作系统挂载表选择最深的真实挂载点。
        var nativePath = RealPath(directory, IntPtr.Zero);
        if (nativePath == IntPtr.Zero) throw NativeError("解析目录真实路径", directory);
        string resolved;
        try { resolved = Marshal.PtrToStringUTF8(nativePath) ?? throw new IOException("真实目录路径为空。"); }
        finally { Free(nativePath); }
        var mount = DriveInfo.GetDrives()
            .Select(drive => Path.TrimEndingDirectorySeparator(drive.Name))
            .Where(path => resolved == path || resolved.StartsWith(path == "/" ? path : path + "/", StringComparison.Ordinal))
            .OrderByDescending(path => path.Length)
            .FirstOrDefault();
        return mount ?? throw new IOException($"无法解析目录所在挂载点：{directory}");
    }

    private static void EnsureDirectoryExists(string directory)
    {
        // 不允许不存在/不可访问目录退回启动卷，防止看似成功的错误容量证据。
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"容量查询目录不存在或不可访问：{directory}");
    }

    private static IOException NativeError(string operation, string directory) =>
        new($"{operation}失败：{directory}", new Win32Exception(Marshal.GetLastWin32Error()));

    [DllImport("kernel32.dll", EntryPoint = "GetVolumePathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);

    [DllImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string directoryName, out ulong availableBytes, out ulong totalBytes, out ulong freeBytes);

    [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
    private static extern IntPtr RealPath([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr resolvedPath);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);
}
