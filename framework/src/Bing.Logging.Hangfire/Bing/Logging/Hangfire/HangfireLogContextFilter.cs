using Bing.Tracing;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Logging.Hangfire;

/// <summary>
/// Hangfire日志上下文过滤器
/// </summary>
public sealed class HangfireLogContextFilter : JobFilterAttribute, IClientFilter, IServerFilter
{
    private const string ContextParameter = "Bing.Logging.Context";
    private const string ScopeItem = "Bing.Logging.Scope";
    private const string RecurringJobIdParameter = "RecurringJobId";
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 初始化一个<see cref="HangfireLogContextFilter"/>类型的实例
    /// </summary>
    public HangfireLogContextFilter(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <inheritdoc />
    public void OnCreating(CreatingContext filterContext)
    {
        if (filterContext == null)
            throw new ArgumentNullException(nameof(filterContext));
        if (!string.IsNullOrWhiteSpace(filterContext.GetJobParameter<string>(RecurringJobIdParameter)))
            return;
        using var serviceScope = _serviceProvider.CreateScope();
        var accessor = serviceScope.ServiceProvider.GetRequiredService<ILogContextAccessor>();
        filterContext.SetJobParameter(ContextParameter, HangfireLogContextData.Serialize(accessor.Capture()));
    }

    /// <inheritdoc />
    public void OnCreated(CreatedContext filterContext)
    {
    }

    /// <inheritdoc />
    public void OnPerforming(PerformingContext filterContext)
    {
        if (filterContext == null)
            throw new ArgumentNullException(nameof(filterContext));
        var serviceScope = _serviceProvider.CreateScope();
        try
        {
            var services = serviceScope.ServiceProvider;
            var generator = services.GetRequiredService<ICorrelationIdGenerator>();
            var accessor = services.GetRequiredService<ILogContextAccessor>();
            var recurringJobId = filterContext.GetJobParameter<string>(RecurringJobIdParameter);
            var snapshot = string.IsNullOrWhiteSpace(recurringJobId)
                ? HangfireLogContextData.Deserialize(filterContext.GetJobParameter<string>(ContextParameter))
                : CreateRecurringSnapshot(generator, recurringJobId);
            snapshot ??= new LogContextSnapshot(generator.Create());
            var logScope = accessor.BeginScope(snapshot);
            filterContext.Items[ScopeItem] = new HangfireLogScope(logScope, serviceScope);
        }
        catch
        {
            serviceScope.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void OnPerformed(PerformedContext filterContext)
    {
        if (filterContext?.Items.TryGetValue(ScopeItem, out var value) == true && value is IDisposable scope)
        {
            filterContext.Items.Remove(ScopeItem);
            scope.Dispose();
        }
    }

    private static LogContextSnapshot CreateRecurringSnapshot(ICorrelationIdGenerator generator, string recurringJobId) =>
        new(
            generator.Create(),
            business: new BusinessLogContext(
                data: new Dictionary<string, object> { [RecurringJobIdParameter] = recurringJobId }));

    private sealed class HangfireLogScope : IDisposable
    {
        private readonly IDisposable _logScope;
        private readonly IServiceScope _serviceScope;
        private bool _disposed;

        public HangfireLogScope(IDisposable logScope, IServiceScope serviceScope)
        {
            _logScope = logScope;
            _serviceScope = serviceScope;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _logScope.Dispose();
            _serviceScope.Dispose();
        }
    }
}