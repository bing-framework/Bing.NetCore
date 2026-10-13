namespace Bing.Caching;

/// <summary>
/// 为非 Redis 缓存提供器提供物理键流式枚举能力。
/// </summary>
/// <remarks>
/// <para>该接口是可选扩展；未实现时，适配器会回退到提供器现有的完整键集合接口。</para>
/// <para>如果提供器同时实现 <see cref="ICacheKeyPageStreamProvider"/>，EasyCaching 优先使用带页边界的接口。</para>
/// <para><c>physicalPrefix</c> 是由 Bing 缓存配置生成的物理命名空间前缀，提供器不得将其当作不可信逻辑键重新拼接。</para>
/// </remarks>
public interface ICacheKeyStreamProvider
{
    /// <summary>
    /// 按物理前缀异步枚举缓存键。
    /// </summary>
    /// <param name="physicalPrefix">Bing 缓存生成的物理命名空间前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按提供器顺序产生的物理键；调用方负责消费速度。</returns>
    IAsyncEnumerable<string> GetAllKeysByPrefixStreamAsync(string physicalPrefix,
        CancellationToken cancellationToken = default);
}
