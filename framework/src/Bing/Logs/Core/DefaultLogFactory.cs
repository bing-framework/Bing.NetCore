using Bing.Logs.Abstractions;

namespace Bing.Logs.Core;

/// <summary>
/// 默认日志工厂
/// </summary>
public class DefaultLogFactory : ILogFactory
{
    /// <summary>
    /// 日志列表
    /// </summary>
    private readonly IEnumerable<ILog> _logs;

    /// <summary>
    /// 初始化一个<see cref="DefaultLogFactory"/>类型的实例
    /// </summary>
    /// <param name="logs">日志列表</param>
    public DefaultLogFactory(IEnumerable<ILog> logs) => _logs = logs;

    /// <summary>
    /// 获取日志
    /// </summary>
    /// <param name="name">日志名称</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public ILog GetLog(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));
        var log = _logs.Where(x => !string.IsNullOrWhiteSpace(x.Name)).FirstOrDefault(x => x.Name.Equals(name));
        if (log == null)
            throw new ArgumentException("找不到匹配的日志操作");
        return log;
    }
}
