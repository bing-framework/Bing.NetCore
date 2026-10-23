using System.Collections.Concurrent;
using Bing.Events;
using Bing.Events.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Events;

/// <summary>
/// 表示一个仅在当前进程内同步处理的 Sample 事件。
/// </summary>
public sealed class SqlUnitOfWorkMemoryEvent : Event
{
    public SqlUnitOfWorkMemoryEvent(string operationId, bool fail = false, bool requireCompletedScope = false)
    {
        OperationId = operationId;
        Fail = fail;
        RequireCompletedScope = requireCompletedScope;
    }

    public string OperationId { get; }
    public bool Fail { get; }
    public bool RequireCompletedScope { get; }
}

/// <summary>
/// 保存由当前 Sample 进程处理的内存事件观察结果。
/// </summary>
public sealed class SqlUnitOfWorkMemoryEventStore
{
    private readonly ConcurrentDictionary<string, SqlUnitOfWorkMemoryEventObservation> _observations = new();

    public void Record(SqlUnitOfWorkMemoryEventObservation observation) =>
        _observations[observation.OperationId] = observation;

    public bool TryGet(string operationId, out SqlUnitOfWorkMemoryEventObservation observation) =>
        _observations.TryGetValue(operationId, out observation!);
}

public sealed record SqlUnitOfWorkMemoryEventObservation(string OperationId, Guid HandlerInstanceId,
    DateTimeOffset HandledAt, bool UnitOfWorkCompleted);

/// <summary>
/// 在当前请求依赖注入范围内解析 handler，避免默认全局 ServiceLocator 跨 scope 复用实例。
/// </summary>
public sealed class ScopedSampleMemoryEventBus : ISimpleEventBus
{
    private readonly IServiceProvider _scopeProvider;

    public ScopedSampleMemoryEventBus(IServiceProvider scopeProvider) => _scopeProvider = scopeProvider;

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent
    {
        foreach (var handler in _scopeProvider.GetServices<IEventHandler<TEvent>>())
            await handler.HandleAsync(@event);
    }
}

/// <summary>
/// 每个 Sample 请求 scope 持有独立的内存事件 handler 实例。
/// </summary>
public sealed class SqlUnitOfWorkMemoryEventHandler : IEventHandler<SqlUnitOfWorkMemoryEvent>
{
    private readonly SqlUnitOfWorkMemoryEventStore _store;
    private readonly Bing.Uow.IUnitOfWorkScopeManager _scopeManager;
    private readonly Guid _instanceId = Guid.NewGuid();

    public SqlUnitOfWorkMemoryEventHandler(SqlUnitOfWorkMemoryEventStore store,
        Bing.Uow.IUnitOfWorkScopeManager scopeManager)
    {
        _store = store;
        _scopeManager = scopeManager;
    }

    public Task HandleAsync(SqlUnitOfWorkMemoryEvent @event)
    {
        if (@event.Fail)
            throw new InvalidOperationException("Sample 内存事件 handler 注入的失败。");
        var completed = _scopeManager.Current?.IsCompleted == true;
        if (@event.RequireCompletedScope && !completed)
            throw new InvalidOperationException("完成回调的内存事件必须在 Uow root 完成后处理。");
        _store.Record(new SqlUnitOfWorkMemoryEventObservation(@event.OperationId, _instanceId,
            DateTimeOffset.UtcNow, completed));
        return Task.CompletedTask;
    }
}
