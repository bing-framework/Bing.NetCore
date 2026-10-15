using Bing.Domain.Entities.Events;

namespace Bing.Domain.Entities;

/// <summary>
/// 验证保存期领域事件的分轮处理与事件源变更检查。
/// </summary>
public sealed class DomainEventDispatchSessionTest
{
    /// <summary>
    /// 验证同一事件实例重复登记时仍分别分发和移除。
    /// </summary>
    [Fact]
    public async Task DispatchAsync_DuplicateEventInstance_ShouldDispatchEachOccurrence()
    {
        var source = new TestSource();
        var @event = new TestEvent();
        source.Add(@event);
        source.Add(@event);
        using var dispatcher = new TestDispatcher();
        var session = new DomainEventDispatchSession(() => new[] { source, source }, dispatcher);

        await session.DispatchAsync();
        session.Complete();

        Assert.Equal(2, dispatcher.DispatchCount);
        Assert.Empty(source.GetDomainEvents());
    }

    /// <summary>
    /// 验证处理器以同长度列表替换事件时不会跳过新事件。
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenHandlerReplacesEvent_ShouldRejectMutation()
    {
        var source = new TestSource();
        source.Add(new TestEvent());
        using var dispatcher = new TestDispatcher(() =>
        {
            source.ClearDomainEvents();
            source.Add(new TestEvent());
        });
        var session = new DomainEventDispatchSession(() => new[] { source }, dispatcher);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => session.DispatchAsync());

        Assert.Contains("替换", exception.Message);
        Assert.Single(source.GetDomainEvents());
    }

    /// <summary>
    /// 验证提交后失败时已分发事件仍被移除。
    /// </summary>
    [Fact]
    public async Task CompleteAfterCommitFailure_WhenRemovalSucceeds_ShouldClearDispatchedEvents()
    {
        var source = new TestSource();
        source.Add(new TestEvent());
        using var dispatcher = new TestDispatcher();
        var session = new DomainEventDispatchSession(() => new[] { source }, dispatcher);
        await session.DispatchAsync();
        var original = new InvalidOperationException("post-commit failure");

        session.CompleteAfterCommitFailure(original);

        Assert.Equal(1, dispatcher.DispatchCount);
        Assert.Empty(source.GetDomainEvents());
        Assert.False(original.Data.Contains("Bing.DomainEventCleanupException"));
    }

    /// <summary>
    /// 验证事件清理失败附加诊断且不替换原始异常。
    /// </summary>
    [Fact]
    public async Task CompleteAfterCommitFailure_WhenRemovalFails_ShouldAttachCleanupError()
    {
        var source = new TestSource();
        source.Add(new TestEvent());
        var cleanup = new InvalidOperationException("remove failed");
        source.RemoveException = cleanup;
        using var dispatcher = new TestDispatcher();
        var session = new DomainEventDispatchSession(() => new[] { source }, dispatcher);
        await session.DispatchAsync();
        var original = new MissingMethodException("flush failed");

        session.CompleteAfterCommitFailure(original);

        Assert.Same(cleanup, original.Data["Bing.DomainEventCleanupException"]);
        Assert.Single(source.GetDomainEvents());
        Assert.Equal(1, dispatcher.DispatchCount);
    }

    /// <summary>
    /// 验证提交后事件清理拒绝空异常。
    /// </summary>
    [Fact]
    public void CompleteAfterCommitFailure_WhenExceptionIsNull_ShouldReject()
    {
        var source = new TestSource();
        using var dispatcher = new TestDispatcher();
        var session = new DomainEventDispatchSession(() => new[] { source }, dispatcher);

        Assert.Throws<ArgumentNullException>(() => session.CompleteAfterCommitFailure(null));
    }

    /// <summary>
    /// 测试事件源。
    /// </summary>
    private sealed class TestSource : IHasDomainEvents
    {
        /// <summary>
        /// 待处理事件。
        /// </summary>
        private readonly List<DomainEvent> _events = new();

        /// <summary>
        /// 获取或设置移除事件时抛出的异常。
        /// </summary>
        public Exception RemoveException { get; set; }

        /// <summary>
        /// 添加待处理事件。
        /// </summary>
        public void Add(DomainEvent @event) => _events.Add(@event);

        /// <inheritdoc />
        public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _events.AsReadOnly();

        /// <inheritdoc />
        public void RemoveDomainEvent(DomainEvent @event)
        {
            if (RemoveException != null)
                throw RemoveException;
            _events.Remove(@event);
        }

        /// <inheritdoc />
        public void ClearDomainEvents() => _events.Clear();
    }

    /// <summary>
    /// 测试领域事件。
    /// </summary>
    private sealed class TestEvent : DomainEvent
    {
    }

    /// <summary>
    /// 测试领域事件分发器。
    /// </summary>
    private sealed class TestDispatcher : IDomainEventDispatcher
    {
        /// <summary>
        /// 事件分发回调。
        /// </summary>
        private readonly Action _onDispatch;

        /// <summary>
        /// 初始化一个 <see cref="TestDispatcher" /> 类型的实例。
        /// </summary>
        public TestDispatcher(Action onDispatch = null) => _onDispatch = onDispatch;

        /// <summary>
        /// 已分发事件数量。
        /// </summary>
        public int DispatchCount { get; private set; }

        /// <inheritdoc />
        public bool Register<TDomainEvent, TDomainEventHandler>()
            where TDomainEvent : DomainEvent
            where TDomainEventHandler : IDomainEventHandler<TDomainEvent> => throw new NotSupportedException();

        /// <inheritdoc />
        public bool Register<TDomainEvent>(Type handlerType) where TDomainEvent : DomainEvent =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public bool Register(Type eventType, Type handlerType) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DispatchAsync(DomainEvent @event)
        {
            DispatchCount++;
            _onDispatch?.Invoke();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
