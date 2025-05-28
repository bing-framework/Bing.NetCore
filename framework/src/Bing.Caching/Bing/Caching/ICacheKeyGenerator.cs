using System.Reflection;

namespace Bing.Caching;

/// <summary>
/// 缓存键生成器
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    /// 创建缓存键
    /// </summary>
    /// <param name="methodInfo">方法</param>
    /// <param name="args">参数</param>
    /// <param name="prefix">缓存键前缀</param>
    /// <returns>根据提供的方法信息、参数和前缀生成的缓存键。</returns>
    string CreateCacheKey(MethodInfo methodInfo, object[] args, string prefix);
}
