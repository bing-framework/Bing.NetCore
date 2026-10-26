namespace Bing.FileStorage.FileSystem;

internal static class LocalPaths
{
    // 不信任来自已有磁盘目录的链接。部署目录仍需操作系统 ACL 保护，以阻止外部进程的检查/使用竞态。
    internal static void CheckLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new FileStorageException(FileStorageError.AccessDenied, "存储路径不得穿过符号链接或重解析点。");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    internal static string Under(string root, params string[] segments)
    {
        var full = Path.GetFullPath(Path.Combine(new[] { root }.Concat(segments).ToArray()));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new FileStorageException(FileStorageError.AccessDenied, "存储路径越界。");
        CheckLinks(full);
        return full;
    }
    internal static void EnsureParent(string path) { CheckLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!); CheckLinks(path); }
    internal static void Capacity(string path, long required)
    {
        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        if (drive.IsReady && required > drive.AvailableFreeSpace - 1024 * 1024)
            throw new FileStorageException(FileStorageError.CapacityExceeded, "本地磁盘剩余容量不足。");
    }
    internal static void DeleteIfPresent(string path)
    {
        try { File.Delete(path); } catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
    }
}
