namespace Bing.FileStorage;

/// <summary>
/// 解析运行时命名实例。
/// </summary>
/// <remarks>只能按名称创建门面，不得同步等待数据库或网络。</remarks>
public interface IFileStorageBackendResolver
{
    /// <summary>
    /// 解析指定名称的文件存储后端。
    /// </summary>
    /// <param name="storeName">命名存储实例名称。</param>
    /// <returns>命名后端；不存在时为 null。</returns>
    IFileStorageBackend? Resolve(string storeName);
}

/// <summary>
/// 后端准入限制。
/// </summary>
/// <remarks>null 表示未提供额外限制，仍须执行业务容器限制。</remarks>
public interface IFileStorageLimitProvider
{
    /// <summary>
    /// 异步获取后端的准入限制。
    /// </summary>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>后端限制；未提供额外限制时为 null。</returns>
    Task<FileStorageLimits?> GetLimitsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 文件存储后端的容量与容器限制。
/// </summary>
public sealed class FileStorageLimits
{
    /// <summary>
    /// 获取或设置单个文件允许的最大字节数。
    /// </summary>
    public long MaximumFileLength { get; set; }
    /// <summary>
    /// 获取或设置是否只允许私有容器。
    /// </summary>
    public bool PrivateContainersOnly { get; set; }
}
