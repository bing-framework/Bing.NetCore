namespace Bing.FileStorage;

/// <summary>可选的单片远端查询能力，避免为核验一个回执读取完整分片清单。</summary>
public interface IMultipartFilePartReader
{
    /// <summary>
    /// 查询指定序号的当前分片；仅分片尚不存在时返回 null。
    /// 会话缺失、权限和网络失败必须抛出异常，结果不得使用缓存回执替代。
    /// </summary>
    Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber, CancellationToken cancellationToken = default);
}

/// <summary>分片查询的兼容入口；第三方 Provider 无需修改已有分片接口。</summary>
public static class MultipartFileStorageExtensions
{
    /// <summary>优先精确查询；旧 Provider 回退到完整清单并按序号唯一匹配。</summary>
    public static async Task<FilePart?> GetPartAsync(this IMultipartFileStorage storage, MultipartUpload upload,
        int partNumber, CancellationToken cancellationToken = default)
    {
        if (storage == null) throw new ArgumentNullException(nameof(storage));
        if (upload == null) throw new ArgumentNullException(nameof(upload));
        upload.GetPartLength(partNumber);
        cancellationToken.ThrowIfCancellationRequested();
        if (storage is IMultipartFilePartReader reader)
            return await reader.GetPartAsync(upload, partNumber, cancellationToken).ConfigureAwait(false);
        var parts = await storage.ListPartsAsync(upload, cancellationToken).ConfigureAwait(false);
        return parts.SingleOrDefault(part => part.Number == partNumber);
    }
}
