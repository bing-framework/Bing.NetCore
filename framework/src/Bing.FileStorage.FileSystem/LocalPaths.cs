namespace Bing.FileStorage.FileSystem;

/// <summary>
/// 提供文件系统存储路径的规范化与安全校验。
/// </summary>
internal static class LocalPaths
{
    // 不信任来自已有磁盘目录的链接。部署目录仍需操作系统 ACL 保护，以阻止外部进程的检查/使用竞态。
    /// <summary>
    /// 检查路径及其父目录不包含重解析点。
    /// </summary>
    /// <param name="path">要检查的文件或目录路径。</param>
    /// <remarks>
    /// 路径中的文件或目录属性无法读取时继续检查其父目录。
    /// </remarks>
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
    /// <summary>
    /// 将路径片段解析到根目录之下并检查链接。
    /// </summary>
    /// <param name="root">允许访问的根目录。</param>
    /// <param name="segments">要组合到根目录下的路径片段。</param>
    /// <returns>位于根目录范围内且经过链接检查的绝对路径。</returns>
    /// <remarks>
    /// 组合结果越出根目录或路径包含重解析点时抛出访问拒绝错误。
    /// </remarks>
    internal static string Under(string root, params string[] segments)
    {
        var full = Path.GetFullPath(Path.Combine(new[] { root }.Concat(segments).ToArray()));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new FileStorageException(FileStorageError.AccessDenied, "存储路径越界。");
        CheckLinks(full);
        return full;
    }
    /// <summary>
    /// 安全创建路径的父目录。
    /// </summary>
    /// <param name="path">要创建父目录的目标路径。</param>
    /// <remarks>
    /// 创建前后均检查路径中的重解析点。
    /// </remarks>
    internal static void EnsureParent(string path) { CheckLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!); CheckLinks(path); }
    /// <summary>
    /// 检查目标磁盘是否保留足够的可用空间。
    /// </summary>
    /// <param name="path">用于确定目标磁盘的路径。</param>
    /// <param name="required">本次操作需要的字节数。</param>
    /// <remarks>
    /// 磁盘已就绪且可用空间减去 1 MiB 后仍小于 required 时抛出容量不足错误。
    /// </remarks>
    internal static void Capacity(string path, long required)
    {
        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        if (drive.IsReady && required > drive.AvailableFreeSpace - 1024 * 1024)
            throw new FileStorageException(FileStorageError.CapacityExceeded, "本地磁盘剩余容量不足。");
    }
    /// <summary>
    /// 删除文件并忽略文件或目录不存在的情况。
    /// </summary>
    /// <param name="path">要删除的文件路径。</param>
    internal static void DeleteIfPresent(string path)
    {
        try { File.Delete(path); } catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
    }
}
