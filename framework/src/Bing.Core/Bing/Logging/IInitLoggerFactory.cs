namespace Bing.Logging;

/// <summary>
/// 初始化日志记录器工厂
/// </summary>
public interface IInitLoggerFactory
{
    /// <summary>
    /// 创建初始化日志记录器
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns>指定类型对应的初始化日志记录器。</returns>
    IInitLogger<T> Create<T>();
}
