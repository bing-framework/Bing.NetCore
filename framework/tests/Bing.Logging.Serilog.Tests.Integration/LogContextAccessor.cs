using Bing.Tracing;

namespace Bing.Logging.Tests;

/// <summary>
/// 日志上下文访问器
/// </summary>
public class LogContextAccessor : Bing.Logging.LogContextAccessor
{
    public LogContextAccessor(ICorrelationIdProvider correlationIdProvider) : base(correlationIdProvider) { }
}