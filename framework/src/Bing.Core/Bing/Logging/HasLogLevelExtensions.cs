using Bing.Helpers;
using Microsoft.Extensions.Logging;

namespace Bing.Logging;

/// <summary>
/// 日志级别(<see cref="IHasLogLevel"/>) 扩展
/// </summary>
public static class HasLogLevelExtensions
{
    /// <summary>
    /// 设置日志级别，并返回该异常
    /// </summary>
    /// <typeparam name="TException">异常类型，必须实现 <see cref="IHasLogLevel"/> 接口。</typeparam>
    /// <param name="exception">要设置日志级别的异常实例。</param>
    /// <param name="logLevel">要分配给异常的 <see cref="LogLevel"/>。</param>
    /// <returns>返回具有指定日志级别的异常对象。</returns>
    /// <exception cref="ArgumentNullException">如果 <paramref name="exception"/> 为空，则抛出异常。</exception>
    public static TException WithLogLevel<TException>(this TException exception, LogLevel logLevel)
        where TException : IHasLogLevel
    {
        Check.NotNull(exception, nameof(exception));
        exception.LogLevel = logLevel;
        return exception;
    }
}
