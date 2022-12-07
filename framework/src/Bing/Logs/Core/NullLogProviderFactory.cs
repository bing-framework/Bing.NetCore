using Bing.Logs.Abstractions;

namespace Bing.Logs.Core;

/// <summary>
/// 空日志提供程序工厂
/// </summary>
public class NullLogProviderFactory : ILogProviderFactory
{
    /// <summary>
    /// 空日志提供程序工厂实例
    /// </summary>
    public static ILogProviderFactory Instance { get; } = new NullLogProviderFactory();

    /// <summary>
    /// 初始化一个<see cref="NullLogProviderFactory"/>类型的实例
    /// </summary>
    private NullLogProviderFactory() { }

    /// <summary>
    /// 创建日志提供程序
    /// </summary>
    /// <param name="logName">日志名称</param>
    /// <param name="format">日志格式化器</param>
    public ILogProvider Create(string logName, ILogFormat format = null) => NullLogProvider.Instance;
}
