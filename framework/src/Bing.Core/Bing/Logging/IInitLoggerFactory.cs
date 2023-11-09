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
    IInitLogger<T> Create<T>();
}
