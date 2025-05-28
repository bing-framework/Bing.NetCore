namespace Bing.Caching;

/// <summary>
/// 缓存键生成器
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    /// 生成缓存键
    /// </summary>
    /// <param name="args">参数</param>
    string GetKey(params object[] args);
}
