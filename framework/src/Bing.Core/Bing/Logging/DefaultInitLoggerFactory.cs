using Bing.Collections;

namespace Bing.Logging;

/// <summary>
/// 初始化日志记录器工厂
/// </summary>
public class DefaultInitLoggerFactory : IInitLoggerFactory
{
    /// <summary>
    /// 按日志目标类型索引初始化日志记录器实例的容器。
    /// </summary>
    private readonly Dictionary<Type, object> _cache = new();

    /// <inheritdoc />
    public virtual IInitLogger<T> Create<T>()
    {
        return (IInitLogger<T>)_cache.GetValueOrDefault(typeof(T), _ => new DefaultInitLogger<T>());
    }
}
