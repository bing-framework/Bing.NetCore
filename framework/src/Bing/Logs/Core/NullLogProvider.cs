using Bing.Logs.Abstractions;

namespace Bing.Logs.Core;

/// <summary>
/// 空日志提供程序
/// </summary>
public class NullLogProvider : ILogProvider
{
    /// <summary>
    /// 空日志提供程序实例
    /// </summary>
    public static ILogProvider Instance { get; } = new NullLogProvider();

    /// <summary>
    /// 日志名称
    /// </summary>
    public string LogName => string.Empty;

    /// <summary>
    /// 调试级别是否启用
    /// </summary>
    public bool IsDebugEnabled => false;

    /// <summary>
    /// 跟踪级别是否启用
    /// </summary>
    public bool IsTraceEnabled => false;

    /// <summary>
    /// 是否分布式日志
    /// </summary>
    public bool IsDistributedLog => false;

    /// <summary>
    /// 初始化一个<see cref="NullLogProvider"/>类型的实例
    /// </summary>
    private NullLogProvider() { }

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="level">日志等级</param>
    /// <param name="content">日志内容</param>
    public void WriteLog(LogLevel level, ILogContent content)
    {
    }
}
