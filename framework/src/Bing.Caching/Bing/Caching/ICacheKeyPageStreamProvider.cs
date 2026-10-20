namespace Bing.Caching;

/// <summary>
/// 为非 Redis 缓存提供器提供带页边界的物理键异步枚举能力。
/// </summary>
/// <remarks>
/// <para>该接口是可选扩展；未实现时，适配器会继续使用逐键流或完整键集合回退。</para>
/// <para>
/// 每个返回项代表提供器已经完成的一页键。提供器应在产生下一页前完成上一页的底层读取，调用方不会保留页引用。
/// </para>
/// <para>
/// <c>physicalPrefix</c> 是由 Bing 缓存配置生成的物理命名空间前缀，提供器不得将其当作不可信逻辑键重新拼接。
/// </para>
/// </remarks>
public interface ICacheKeyPageStreamProvider
{
    /// <summary>
    /// 按物理前缀异步枚举缓存键页。
    /// </summary>
    /// <param name="physicalPrefix">Bing 缓存生成的物理命名空间前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按提供器顺序产生的键页；调用方负责消费速度，页内顺序由提供器定义。</returns>
    IAsyncEnumerable<IReadOnlyList<string>> GetAllKeyPagesByPrefixStreamAsync(string physicalPrefix,
        CancellationToken cancellationToken = default);
}
