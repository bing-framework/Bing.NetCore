namespace Bing.FileStorage;

/// <summary>
/// 可选的单片远端查询能力，避免为核验一个回执读取完整分片清单。
/// </summary>
public interface IMultipartFilePartReader
{
    /// <summary>
    /// 查询指定序号的当前分片。
    /// </summary>
    /// <remarks>仅分片尚不存在时返回 null。
    /// 会话缺失、权限和网络失败必须抛出异常，结果不得使用缓存回执替代。</remarks>
    /// <param name="upload">分片上传会话。</param>
    /// <param name="partNumber">分片序号。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>指定分片的当前回执；分片不存在时为 null。</returns>
    Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber, CancellationToken cancellationToken = default);
}

/// <summary>
/// 兼容不同分片读取能力的文件存储扩展。
/// </summary>
/// <remarks>
/// 分片查询的兼容入口；第三方 Provider 无需修改已有分片接口。
/// </remarks>
public static class MultipartFileStorageExtensions
{
    /// <summary>
    /// 获取指定分片的内容信息。
    /// </summary>
    /// <param name="storage">提供分片读取能力的存储。</param>
    /// <param name="upload">包含目标文件和分片大小的上传会话。</param>
    /// <param name="partNumber">要读取的分片编号。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>对应的文件分片；分片不存在时为 null。</returns>
    /// <remarks>
    /// 存储支持单分片读取时优先使用该能力，否则查询分片列表。
    /// </remarks>
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
