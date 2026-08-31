namespace Bing.Logging;

/// <summary>
/// 按日志类别创建日志操作对象。
/// </summary>
public interface ILogFactory
{
    /// <summary>
    /// 按显式日志类别名称创建日志操作对象。
    /// </summary>
    /// <param name="categoryName">用于日志筛选和归属的类别名称。</param>
    /// <returns>关联指定类别名称的日志操作对象。</returns>
    ILog CreateLog(string categoryName);

    /// <summary>
    /// 按类型创建日志操作对象。
    /// </summary>
    /// <param name="type">用于确定日志类别名称的类型。</param>
    /// <returns>关联指定类型类别名称的日志操作对象。</returns>
    ILog CreateLog(Type type);
}
