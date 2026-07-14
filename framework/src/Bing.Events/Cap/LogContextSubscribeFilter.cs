using Bing.Logging;
using Bing.Tracing;
using DotNetCore.CAP.Filter;

namespace Bing.Events.Cap;

/// <summary>
/// CAP订阅日志上下文过滤器
/// </summary>
internal sealed class LogContextSubscribeFilter : SubscribeFilter
{
    private readonly ILogContextAccessor _logContextAccessor;
    private readonly ICorrelationIdGenerator _correlationIdGenerator;
    private IDisposable _scope;

    public LogContextSubscribeFilter(
        ILogContextAccessor logContextAccessor,
        ICorrelationIdGenerator correlationIdGenerator)
    {
        _logContextAccessor = logContextAccessor;
        _correlationIdGenerator = correlationIdGenerator;
    }

    /// <inheritdoc />
    public override Task OnSubscribeExecutingAsync(ExecutingContext context)
    {
        DisposeScope();
        var headers = context.DeliverMessage.Headers.ToDictionary(x => x.Key, x => x.Value);
        var snapshot = CapLogContextHeaders.Read(headers, _correlationIdGenerator.Create());
        _scope = _logContextAccessor.BeginScope(snapshot);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnSubscribeExecutedAsync(ExecutedContext context)
    {
        DisposeScope();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnSubscribeExceptionAsync(ExceptionContext context)
    {
        DisposeScope();
        return Task.CompletedTask;
    }

    private void DisposeScope()
    {
        _scope?.Dispose();
        _scope = null;
    }
}