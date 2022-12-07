using Bing.AspNetCore.Logs;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// 日志工厂(<see cref="ILoggerFactory"/>) 扩展
/// </summary>
public static partial class LoggerFactoryExtensions
{
    /// <summary>
    /// 添加系统日志提供程序。用于接管ILogger日志
    /// </summary>
    /// <param name="loggerFactory">日志工厂</param>
    public static void AddSysLogProvider(this ILoggerFactory loggerFactory) => loggerFactory.AddProvider(new SysLoggerProvider());
}