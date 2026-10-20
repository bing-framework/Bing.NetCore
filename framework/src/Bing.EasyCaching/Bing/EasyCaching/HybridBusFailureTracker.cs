using EasyCaching.Core.Bus;
using System.Runtime.ExceptionServices;
using StackExchange.Redis;

namespace Bing.EasyCaching;

/// <summary>
/// 跟踪 Hybrid 总线发布操作的结果。
/// </summary>
/// <remarks>用于向调用方传播 Provider 内部捕获的发布异常。</remarks>
internal sealed class HybridBusFailureTracker : IEasyCachingBus, IDisposable
{
    /// <summary>
    /// 被包装的 EasyCaching 总线。
    /// </summary>
    private readonly IEasyCachingBus _inner;
    /// <summary>
    /// 总线重连时执行的回调。
    /// </summary>
    private readonly Action _onReconnect;
    /// <summary>
    /// 用于监听 Redis 连接状态的多路复用器。
    /// </summary>
    private readonly IConnectionMultiplexer _multiplexer;
    /// <summary>
    /// 当前异步执行流中的发布操作。
    /// </summary>
    private readonly AsyncLocal<PublishOperation> _operation = new();
    /// <summary>
    /// 当前实例发布消息使用的来源标记。
    /// </summary>
    private readonly string _publisherPrefix = Guid.NewGuid().ToString("N") + ":";
    /// <summary>
    /// 收到其他实例失效消息时执行的回调。
    /// </summary>
    private Action _onInvalidation;

    /// <summary>
    /// 初始化一个 <see cref="HybridBusFailureTracker"/> 类型的实例。
    /// </summary>
    /// <param name="inner">被包装的 EasyCaching 总线。</param>
    /// <param name="onReconnect">连接恢复时执行的回调。</param>
    /// <param name="multiplexer">可选的 Redis 连接多路复用器。</param>
    internal HybridBusFailureTracker(IEasyCachingBus inner, Action onReconnect = null,
        IConnectionMultiplexer multiplexer = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _onReconnect = onReconnect;
        _multiplexer = multiplexer;
        if (_multiplexer != null)
        {
            _multiplexer.ConnectionFailed += OnConnectionChanged;
            _multiplexer.ConnectionRestored += OnConnectionChanged;
        }
    }

    /// <summary>
    /// 处理 Redis 连接状态变化。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="args">连接状态事件数据。</param>
    private void OnConnectionChanged(object sender, ConnectionFailedEventArgs args)
    {
        if (args.ConnectionType == ConnectionType.Subscription) _onReconnect?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_multiplexer == null) return;
        _multiplexer.ConnectionFailed -= OnConnectionChanged;
        _multiplexer.ConnectionRestored -= OnConnectionChanged;
        // 客户端由底层 Provider/DI 拥有，这里只退订事件。
    }

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <summary>
    /// 开始跟踪一次发布操作。
    /// </summary>
    /// <returns>当前执行流的发布操作上下文。</returns>
    internal PublishOperation BeginOperation()
    {
        var operation = new PublishOperation(this, _operation.Value);
        _operation.Value = operation;
        return operation;
    }

    /// <summary>
    /// 设置收到失效消息时调用的观察者。
    /// </summary>
    /// <param name="observer">失效消息观察者。</param>
    internal void SetInvalidationObserver(Action observer) =>
        Volatile.Write(ref _onInvalidation, observer);

    /// <inheritdoc />
    public void Publish(string topic, EasyCachingMessage message)
    {
        MarkOrigin(message);
        try
        {
            _inner.Publish(topic, message);
            _operation.Value?.RecordSuccess();
        }
        catch (Exception error)
        {
            _operation.Value?.RecordFailure(error);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(string topic, EasyCachingMessage message,
        CancellationToken cancellationToken = default)
    {
        MarkOrigin(message);
        try
        {
            await _inner.PublishAsync(topic, message, cancellationToken).ConfigureAwait(false);
            _operation.Value?.RecordSuccess();
        }
        catch (Exception error)
        {
            _operation.Value?.RecordFailure(error);
            throw;
        }
    }

    /// <inheritdoc />
    public void Subscribe(string topic, Action<EasyCachingMessage> handler, Action onConnect) =>
        _inner.Subscribe(topic, message =>
        {
            if (IsOwnMessage(message)) return;
            Volatile.Read(ref _onInvalidation)?.Invoke();
            handler(message);
        },
            () => { _onReconnect?.Invoke(); onConnect?.Invoke(); });

    /// <inheritdoc />
    public Task SubscribeAsync(string topic, Action<EasyCachingMessage> handler, Action onConnect,
        CancellationToken cancellationToken = default) =>
        _inner.SubscribeAsync(topic, message =>
        {
            if (IsOwnMessage(message)) return;
            Volatile.Read(ref _onInvalidation)?.Invoke();
            handler(message);
        },
            () => { _onReconnect?.Invoke(); onConnect?.Invoke(); }, cancellationToken);

    // 发布者主动更新/删除本地层；自身通知回环不得把刚回填的值再次驱逐。
    // 固定实例前缀无需保存消息 ID 集合，因此不会随发布数量增长内存。
    /// <summary>
    /// 判断消息是否由当前实例发布。
    /// </summary>
    /// <param name="message">待检查的总线消息。</param>
    /// <returns>消息来自当前实例时返回 <see langword="true"/>。</returns>
    private bool IsOwnMessage(EasyCachingMessage message) =>
        message?.Id?.StartsWith(_publisherPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// 标记消息的发布来源。
    /// </summary>
    /// <param name="message">待标记的总线消息。</param>
    private void MarkOrigin(EasyCachingMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (!IsOwnMessage(message)) message.Id = _publisherPrefix + message.Id;
    }

    /// <summary>
    /// 跟踪一次总线发布及其失败信息。
    /// </summary>
    internal sealed class PublishOperation : IDisposable
    {
        /// <summary>
        /// 拥有此发布操作的跟踪器。
        /// </summary>
        private readonly HybridBusFailureTracker _owner;
        /// <summary>
        /// 进入该操作前的发布上下文。
        /// </summary>
        private readonly PublishOperation _previous;
        /// <summary>
        /// 最近一次发布失败的信息。
        /// </summary>
        private ExceptionDispatchInfo _failure;

        /// <summary>
        /// 初始化一个 <see cref="PublishOperation"/> 类型的实例。
        /// </summary>
        /// <param name="owner">拥有此操作的跟踪器。</param>
        /// <param name="previous">上一个发布操作上下文。</param>
        internal PublishOperation(HybridBusFailureTracker owner, PublishOperation previous)
        {
            _owner = owner;
            _previous = previous;
        }

        /// <summary>
        /// 记录发布成功。
        /// </summary>
        internal void RecordSuccess() => _failure = null;

        /// <summary>
        /// 记录发布失败。
        /// </summary>
        /// <param name="error">发布异常。</param>
        internal void RecordFailure(Exception error) => _failure = ExceptionDispatchInfo.Capture(error);

        /// <summary>
        /// 重新抛出已记录的发布异常。
        /// </summary>
        internal void ThrowIfFailed() => _failure?.Throw();

        /// <inheritdoc />
        public void Dispose() => _owner._operation.Value = _previous;
    }
}
