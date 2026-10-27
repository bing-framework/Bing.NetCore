namespace Bing.Biz.FileManagement;

/// <summary>仅供读取授权使用的快照，不含上传会话，不能作为写入聚合使用。</summary>
public sealed class FileReadSnapshot
{
    public FileRecord File { get; set; } = new();
    public IReadOnlyList<FileBinding> Bindings { get; set; } = Array.Empty<FileBinding>();
}

/// <summary>可选的批量读取能力；不改变第三方 IFileManagementStore 的既有实现契约。</summary>
public interface IFileReadBatchStore
{
    /// <summary>
    /// 在同一短事务中读取指定范围的 Ready 文件及其关联，不加载上传会话。
    /// 每批输入最多 100 个 ID；重复 ID 去重，空批不读库；缺失、非 Ready、其他范围的记录不返回。
    /// 返回脱离跟踪的快照，不保证顺序；参数和取消应在读取之前校验。
    /// </summary>
    Task<IReadOnlyList<FileReadSnapshot>> ReadReadyFilesAsync(string scopeKey, IReadOnlyCollection<Guid> fileIds,
        CancellationToken ct = default);
}
