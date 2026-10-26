namespace Bing.FileStorage.FileSystem;

/// <summary>离线回收进程崩溃遗留的临时文件。调用前停止共享该根目录的所有写入进程。</summary>
public static class FileSystemStorageMaintenance
{
    /// <summary>
    /// 只清理由 Provider 创建、早于截止时间的 .tmp 文件，不删除正式 .blob、分片或会话清单。
    /// apply=false 返回预览；任何链接路径都会被拒绝。正常过期上传由业务层中止并清理分片。
    /// </summary>
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
