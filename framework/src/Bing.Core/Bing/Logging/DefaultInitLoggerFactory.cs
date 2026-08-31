using Bing.Collections;

namespace Bing.Logging;

/// <summary>
/// 初始化日志记录器工厂
/// </summary>
public class DefaultInitLoggerFactory : IInitLoggerFactory
{
    /// <summary>
    /// 缓存
    /// </summary>
    private readonly Dictionary<Type, object> _cache = new();

    /// <summary>
    /// 创建初始化日志记录器
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns>指定类型对应的初始化日志记录器。</returns>
    public virtual IInitLogger<T> Create<T>()
    {
        return (IInitLogger<T>)_cache.GetValueOrDefault(typeof(T), _ => new DefaultInitLogger<T>());
    }
}
