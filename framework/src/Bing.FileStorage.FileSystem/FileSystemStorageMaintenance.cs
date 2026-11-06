namespace Bing.FileStorage.FileSystem;

/// <summary>
/// 本地文件存储临时文件离线回收工具。
/// </summary>
/// <remarks>
/// 离线回收进程崩溃遗留的临时文件。调用前停止共享该根目录的所有写入进程。
/// </remarks>
public static class FileSystemStorageMaintenance
{
    /// <summary>
    /// 查找并可选删除过期的文件存储临时文件。
    /// </summary>
    /// <param name="rootPath">文件存储根目录。</param>
    /// <param name="storageNamespace">文件存储命名空间。</param>
    /// <param name="olderThan">临时文件的过期时间界限。</param>
    /// <param name="apply">是否实际删除找到的文件。</param>
    /// <returns>符合过期条件的临时文件完整路径列表。</returns>
    /// <remarks>
    /// 仅处理至少过期 24 小时且文件名包含 N 格式 Guid 的 .tmp 文件；apply 为 false 时只返回结果。
    /// </remarks>
    public static IReadOnlyList<string> CleanupTemporaryFiles(string rootPath, string storageNamespace, DateTimeOffset olderThan, bool apply)
    {
        if (!Path.IsPathFullyQualified(rootPath)) throw new ArgumentException("需要绝对根目录。", nameof(rootPath));
        if (olderThan > DateTimeOffset.UtcNow.AddHours(-24)) throw new ArgumentException("至少保留 24 小时的临时文件。", nameof(olderThan));
        var directory = LocalPaths.Under(Path.GetFullPath(rootPath), FileStorageNames.ValidateIdentifier(storageNamespace, nameof(storageNamespace)));
        if (!Directory.Exists(directory)) return Array.Empty<string>();
        var found = new List<string>();
        // 不跟随链接；逐个访问前再次检查祖先路径。此维护操作要求目录离线，不能与写入并发。
        var pending = new Stack<string>(); pending.Push(directory);
        while (pending.Count > 0)
        {
            var current = pending.Pop(); LocalPaths.CheckLinks(current);
            foreach (var child in Directory.EnumerateDirectories(current)) { LocalPaths.CheckLinks(child); pending.Push(child); }
            foreach (var file in Directory.EnumerateFiles(current, "*.tmp"))
            {
                LocalPaths.CheckLinks(file);
                if (File.GetLastWriteTimeUtc(file) >= olderThan.UtcDateTime) continue;
                // 仅匹配当前格式产生的 GUID 临时名。
                var name = Path.GetFileName(file);
                if (!name.Split('.').Any(part => Guid.TryParseExact(part, "N", out _))) continue;
                found.Add(file);
                if (apply) File.Delete(file);
            }
        }
        return found;
    }
}
