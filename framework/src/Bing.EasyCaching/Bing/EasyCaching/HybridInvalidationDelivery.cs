using Bing.Caching;
using EasyCaching.Core.Bus;
using EasyCaching.Core.Serialization;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

namespace Bing.EasyCaching;

/// <summary>
/// 统一处理 Hybrid 失效消息的有界重试和可选 Redis outbox。
/// </summary>
internal sealed class HybridInvalidationDelivery : IDisposable,
    IHybridInvalidationDeliveryDiagnostics, IHybridInvalidationDeadLetterAdmin
{
    /// <summary>
    /// 管理 Hybrid 失效总线及其故障状态。
    /// </summary>
    private readonly HybridBusFailureTracker _bus;
    /// <summary>
    /// 序列化失效消息的 EasyCaching 序列化器。
    /// </summary>
    private readonly IEasyCachingSerializer _serializer;
    /// <summary>
    /// 当前失效投递策略和容量配置。
    /// </summary>
    private readonly HybridInvalidationDeliveryOptions _options;
    /// <summary>
    /// 可选的 Redis 持久化 outbox。
    /// </summary>
    private readonly IHybridInvalidationOutbox _outbox;
    /// <summary>
    /// dead-letter 管理操作的可选授权器。
    /// </summary>
    private readonly IHybridInvalidationDeadLetterAuthorizer _deadLetterAuthorizer;
    /// <summary>
    /// dead-letter 管理操作的可选审计接收器。
    /// </summary>
    private readonly IHybridInvalidationDeadLetterAuditSink _deadLetterAuditSink;
    /// <summary>
    /// 通知 outbox 后台工作线程队列状态变化的信号量。
    /// </summary>
    private readonly SemaphoreSlim _signal = null;
    /// <summary>
    /// 停止 outbox 后台工作线程的取消源。
    /// </summary>
    private readonly CancellationTokenSource _stop = null;
    /// <summary>
    /// 负责重试 outbox 消息的后台工作任务。
    /// </summary>
    private readonly Task _worker = null;
    /// <summary>
    /// 保护最近一次失败诊断信息的同步对象。
    /// </summary>
    private readonly object _diagnosticsGate = new object();
    /// <summary>
    /// 成功发布的失效消息数。
    /// </summary>
    private long _successfulPublishCount;
    /// <summary>
    /// 失败的发布尝试次数。
    /// </summary>
    private long _failedPublishAttemptCount;
    /// <summary>
    /// 成功加入 outbox 的消息数。
    /// </summary>
    private long _enqueuedMessageCount;
    /// <summary>
    /// 移入 dead-letter 的消息数。
    /// </summary>
    private long _deadLetteredMessageCount;
    /// <summary>
    /// 清理的过期 dead-letter 消息数。
    /// </summary>
    private long _expiredDeadLetterCount;
    /// <summary>
    /// 最近一次投递失败时间。
    /// </summary>
    private DateTimeOffset? _lastFailureUtc;
    /// <summary>
    /// 最近一次投递失败摘要。
    /// </summary>
    private string _lastFailureMessage;
    /// <summary>
    /// 下次清理过期 dead-letter 的最早执行时间。
    /// </summary>
    private DateTimeOffset _nextDeadLetterCleanupUtc;
    /// <summary>
    /// 记录当前投递器是否已释放；非零表示已释放。
    /// </summary>
    private int _disposed;

    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationDelivery"/> 类型的实例。
    /// </summary>
    /// <param name="bus">失效消息总线故障跟踪器。</param>
    /// <param name="serializer">消息序列化器。</param>
    /// <param name="configuration">缓存实例配置。</param>
    /// <param name="topic">失效消息主题。</param>
    /// <param name="database">可选的 Redis 数据库，用于创建持久化 outbox。</param>
    /// <param name="options">投递、重试和容量配置。</param>
    /// <param name="outbox">可选的 outbox 实现。</param>
    /// <param name="deadLetterAuthorizer">可选的 dead-letter 管理授权器。</param>
    /// <param name="deadLetterAuditSink">可选的 dead-letter 管理审计接收器。</param>
    internal HybridInvalidationDelivery(HybridBusFailureTracker bus, IEasyCachingSerializer serializer,
        CacheConfiguration configuration, string topic, IDatabase database,
        HybridInvalidationDeliveryOptions options, IHybridInvalidationOutbox outbox = null,
        IHybridInvalidationDeadLetterAuthorizer deadLetterAuthorizer = null,
        IHybridInvalidationDeadLetterAuditSink deadLetterAuditSink = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("失效消息主题不能为空。", nameof(topic));
        _options = (options ?? new HybridInvalidationDeliveryOptions()).CloneValidated();

        if (outbox != null && _options.MaxPendingMessages == 0)
            throw new ArgumentException("注入 outbox 时必须配置正的 MaxPendingMessages。", nameof(options));
        if (outbox == null && _options.MaxPendingMessages > 0)
        {
            if (database == null)
                throw new NotSupportedException("启用 Hybrid Redis outbox 需要分布式 Provider 提供 StackExchange.Redis 数据库。");
            outbox = new RedisHybridInvalidationOutbox(database, configuration,
                _options.MaxPendingMessages, _options.DeadLetterMaxMessages,
                _options.DeadLetterMaxBytes);
        }

        Topic = topic;
        _outbox = outbox;
        _deadLetterAuthorizer = deadLetterAuthorizer;
        _deadLetterAuditSink = deadLetterAuditSink;
        _nextDeadLetterCleanupUtc = DateTimeOffset.MinValue;
        if (_outbox == null)
            return;

        _stop = new CancellationTokenSource();
        _signal = new SemaphoreSlim(0);
        _worker = Task.Run(DrainLoopAsync);
    }

    /// <summary>
    /// 获取当前失效消息主题。
    /// </summary>
    internal string Topic { get; }

    /// <inheritdoc />
    public HybridInvalidationDeliverySnapshot GetSnapshot()
    {
        ThrowIfDisposed();
        return CreateSnapshot(_outbox?.GetStatus());
    }

    /// <inheritdoc />
    public async Task<HybridInvalidationDeliverySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var status = _outbox == null
            ? null
            : await _outbox.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return CreateSnapshot(status);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HybridInvalidationDeadLetterEntry>> GetDeadLettersAsync(
        string actor, int maximumMessages = 100,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateActor(actor);
        ValidateAdminBatchSize(maximumMessages);
        var outbox = GetDeadLetterAdminOutbox();
        cancellationToken.ThrowIfCancellationRequested();
        var request = new HybridInvalidationDeadLetterAuthorizationRequest(
            HybridInvalidationDeadLetterAction.View, Topic, actor, null, maximumMessages);
        await AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await outbox.ReadDeadLettersAsync(maximumMessages, cancellationToken)
                .ConfigureAwait(false);
            var entries = new List<HybridInvalidationDeadLetterEntry>(records.Count);
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(CreateDeadLetterEntry(record));
            }
            await AuditAsync(new HybridInvalidationDeadLetterAuditEntry(
                HybridInvalidationDeadLetterAction.View, Topic, actor, null, true,
                entries.Count, DateTimeOffset.UtcNow, null), cancellationToken).ConfigureAwait(false);
            return entries;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            await RecordAdminFailureAsync(request, error).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ReplayAsync(HybridInvalidationDeadLetterEntry entry,
        string actor, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateEntry(entry);
        return await ReplayAsync(entry.Handle, actor, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ReplayAsync(string handle, string actor,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateHandle(handle);
        ValidateActor(actor);
        var outbox = GetDeadLetterAdminOutbox();
        cancellationToken.ThrowIfCancellationRequested();
        var request = new HybridInvalidationDeadLetterAuthorizationRequest(
            HybridInvalidationDeadLetterAction.Replay, Topic, actor, null, 1, handle);
        await AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = await outbox.GetDeadLetterPayloadAsync(handle, cancellationToken)
                .ConfigureAwait(false);
            if (payload == null)
            {
                await AuditAsync(new HybridInvalidationDeadLetterAuditEntry(
                    HybridInvalidationDeadLetterAction.Replay, Topic, actor, null, false, 0,
                    DateTimeOffset.UtcNow, "dead-letter 不存在"), cancellationToken)
                    .ConfigureAwait(false);
                return false;
            }

            var entry = CreateDeadLetterEntry(new HybridInvalidationDeadLetterRecord(
                payload, null, handle));
            request = new HybridInvalidationDeadLetterAuthorizationRequest(
                HybridInvalidationDeadLetterAction.Replay, Topic, actor, entry.MessageId, 1, handle);
            if (entry.Malformed)
                throw new InvalidOperationException("无法重放格式损坏的 Hybrid dead-letter 消息。");

            var replayed = await outbox.ReplayDeadLetterAsync(handle, cancellationToken)
                .ConfigureAwait(false);
            await AuditAsync(new HybridInvalidationDeadLetterAuditEntry(
                HybridInvalidationDeadLetterAction.Replay, Topic, actor, entry.MessageId,
                replayed, replayed ? 1 : 0, DateTimeOffset.UtcNow,
                replayed ? null : "dead-letter 不存在或 pending/processing 已达到容量上限"),
                cancellationToken).ConfigureAwait(false);
            return replayed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            await RecordAdminFailureAsync(request, error).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmDeleteAsync(HybridInvalidationDeadLetterEntry entry,
        string confirmationToken, string actor,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateEntry(entry);
        return await ConfirmDeleteAsync(entry.Handle, confirmationToken, actor, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmDeleteAsync(string handle, string confirmationToken,
        string actor, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateHandle(handle);
        ValidateActor(actor);
        if (string.IsNullOrWhiteSpace(confirmationToken))
            throw new ArgumentException("dead-letter 确认 token 不能为空。", nameof(confirmationToken));
        var outbox = GetDeadLetterAdminOutbox();
        cancellationToken.ThrowIfCancellationRequested();
        var request = new HybridInvalidationDeadLetterAuthorizationRequest(
            HybridInvalidationDeadLetterAction.ConfirmDelete, Topic, actor, null, 1, handle);
        await AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = await outbox.GetDeadLetterPayloadAsync(handle, cancellationToken)
                .ConfigureAwait(false);
            if (payload == null)
            {
                await AuditAsync(new HybridInvalidationDeadLetterAuditEntry(
                    HybridInvalidationDeadLetterAction.ConfirmDelete, Topic, actor, null,
                    false, 0, DateTimeOffset.UtcNow, "dead-letter 不存在"), cancellationToken)
                    .ConfigureAwait(false);
                return false;
            }

            var entry = CreateDeadLetterEntry(new HybridInvalidationDeadLetterRecord(
                payload, null, handle));
            request = new HybridInvalidationDeadLetterAuthorizationRequest(
                HybridInvalidationDeadLetterAction.ConfirmDelete, Topic, actor,
                entry.MessageId, 1, handle);
            ValidateConfirmationToken(entry, payload, confirmationToken);

            var deleted = await outbox.DeleteDeadLetterAsync(handle, cancellationToken)
                .ConfigureAwait(false);
            await AuditAsync(new HybridInvalidationDeadLetterAuditEntry(
                HybridInvalidationDeadLetterAction.ConfirmDelete, Topic, actor, entry.MessageId,
                deleted, deleted ? 1 : 0, DateTimeOffset.UtcNow,
                deleted ? null : "dead-letter 不存在"), cancellationToken).ConfigureAwait(false);
            return deleted;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            await RecordAdminFailureAsync(request, error).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 按载荷预算拆分并同步发布一批失效键。
    /// </summary>
    /// <param name="physicalKeys">需要广播失效的物理键。</param>
    /// <param name="maximumBytes">单条消息最大载荷字节数；零表示不限制。</param>
    internal void Publish(IReadOnlyList<string> physicalKeys, long maximumBytes)
    {
        ThrowIfDisposed();
        foreach (var message in HybridInvalidationMessageBatcher.CreateMessages(
                     _serializer, physicalKeys, maximumBytes))
            PublishOne(Topic, message);
    }

    /// <summary>
    /// 按载荷预算拆分并异步发布一批失效键。
    /// </summary>
    /// <param name="physicalKeys">需要广播失效的物理键。</param>
    /// <param name="maximumBytes">单条消息最大载荷字节数；零表示不限制。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    internal async Task PublishAsync(IReadOnlyList<string> physicalKeys, long maximumBytes,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        foreach (var message in HybridInvalidationMessageBatcher.CreateMessages(
                     _serializer, physicalKeys, maximumBytes))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PublishOneAsync(Topic, message, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 重试并同步发布一条失效消息。
    /// </summary>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">待发布的失效消息。</param>
    private void PublishOne(string topic, EasyCachingMessage message)
    {
        Exception failure = null;
        for (var attempt = 1; attempt <= _options.MaxPublishAttempts; attempt++)
        {
            try
            {
                _bus.Publish(topic, message);
                Interlocked.Increment(ref _successfulPublishCount);
                return;
            }
            catch (Exception error)
            {
                RecordFailure(error, countPublishAttempt: true);
                failure = error;
                if (attempt < _options.MaxPublishAttempts && _options.RetryDelay > TimeSpan.Zero)
                    Thread.Sleep(_options.RetryDelay);
            }
        }

        EnqueueAfterFailure(topic, message);
        throw failure ?? new InvalidOperationException("Hybrid 失效消息发布失败。");
    }

    /// <summary>
    /// 重试并异步发布一条失效消息。
    /// </summary>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">待发布的失效消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task PublishOneAsync(string topic, EasyCachingMessage message,
        CancellationToken cancellationToken)
    {
        Exception failure = null;
        for (var attempt = 1; attempt <= _options.MaxPublishAttempts; attempt++)
        {
            try
            {
                await _bus.PublishAsync(topic, message, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _successfulPublishCount);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                RecordFailure(error, countPublishAttempt: true);
                failure = error;
                if (attempt < _options.MaxPublishAttempts && _options.RetryDelay > TimeSpan.Zero)
                    await Task.Delay(_options.RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        await EnqueueAfterFailureAsync(topic, message, cancellationToken).ConfigureAwait(false);
        throw failure ?? new InvalidOperationException("Hybrid 失效消息发布失败。");
    }

    /// <summary>
    /// 发布失败后尝试将消息加入持久化 outbox。
    /// </summary>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">发布失败的失效消息。</param>
    private void EnqueueAfterFailure(string topic, EasyCachingMessage message)
    {
        if (_outbox == null)
            return;
        try
        {
            if (_outbox.TryEnqueue(topic, message))
            {
                Interlocked.Increment(ref _enqueuedMessageCount);
                _signal.Release();
            }
        }
        catch (Exception error)
        {
            RecordFailure(error, countPublishAttempt: false);
            // 保留总线的原始异常；outbox 故障不能掩盖当前缓存操作失败原因。
        }
    }

    /// <summary>
    /// 发布失败后异步尝试将消息加入持久化 outbox。
    /// </summary>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">发布失败的失效消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task EnqueueAfterFailureAsync(string topic, EasyCachingMessage message,
        CancellationToken cancellationToken)
    {
        if (_outbox == null)
            return;
        try
        {
            if (await _outbox.TryEnqueueAsync(topic, message, cancellationToken).ConfigureAwait(false))
            {
                Interlocked.Increment(ref _enqueuedMessageCount);
                _signal.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            RecordFailure(error, countPublishAttempt: false);
            // 保留总线的原始异常；outbox 故障不能掩盖当前缓存操作失败原因。
        }
    }

    /// <summary>
    /// 轮询并发布 outbox 中待重试的失效消息。
    /// </summary>
    private async Task DrainLoopAsync()
    {
        var cancellationToken = _stop.Token;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(GetDrainWaitInterval(), cancellationToken)
                    .ConfigureAwait(false);
                await CleanupExpiredDeadLettersAsync(cancellationToken).ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested)
                {
                    var item = await _outbox.TryTakeAsync(cancellationToken).ConfigureAwait(false);
                    if (item == null)
                        break;
                    var publishCompleted = false;
                    try
                    {
                        await PublishDirectAsync(item.Topic, item.Message, cancellationToken)
                            .ConfigureAwait(false);
                        publishCompleted = true;
                        await _outbox.AcknowledgeAsync(item, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception error)
                    {
                        if (!publishCompleted && ShouldDeadLetter(item))
                        {
                            try
                            {
                                if (await _outbox.TryDeadLetterAsync(item, cancellationToken)
                                        .ConfigureAwait(false))
                                {
                                    Interlocked.Increment(ref _deadLetteredMessageCount);
                                    continue;
                                }
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }
                            catch (Exception deadLetterError)
                            {
                                RecordFailure(deadLetterError, countPublishAttempt: false);
                            }
                        }
                        if (publishCompleted)
                            RecordFailure(error, countPublishAttempt: false);
                        // 消息继续留在 processing，下一次轮询或进程重启后重试。
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                RecordFailure(error, countPublishAttempt: false);
                // Redis 或总线暂时不可用时保留 outbox，等待下一轮轮询。
            }
        }
    }

    /// <summary>
    /// 对 outbox 消息执行有界异步发布重试。
    /// </summary>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">待发布的失效消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task PublishDirectAsync(string topic, EasyCachingMessage message,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxPublishAttempts; attempt++)
        {
            try
            {
                await _bus.PublishAsync(topic, message, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _successfulPublishCount);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                RecordFailure(error, countPublishAttempt: true);
                if (attempt >= _options.MaxPublishAttempts)
                    throw;
                if (_options.RetryDelay > TimeSpan.Zero)
                    await Task.Delay(_options.RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 按保留期限清理过期 dead-letter 消息。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task CleanupExpiredDeadLettersAsync(CancellationToken cancellationToken)
    {
        if (_outbox == null || _options.DeadLetterRetention <= TimeSpan.Zero)
            return;

        var now = DateTimeOffset.UtcNow;
        if (now < _nextDeadLetterCleanupUtc)
            return;
        _nextDeadLetterCleanupUtc = now + _options.DeadLetterCleanupInterval;
        try
        {
            var removed = await _outbox.CleanupExpiredDeadLettersAsync(
                now - _options.DeadLetterRetention,
                _options.DeadLetterCleanupBatchSize,
                cancellationToken).ConfigureAwait(false);
            if (removed > 0)
                Interlocked.Add(ref _expiredDeadLetterCount, removed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            RecordFailure(error, countPublishAttempt: false);
            // 清理失败不影响 processing 消息继续保留和后续重试。
        }
    }

    /// <summary>
    /// 获取 outbox 后台轮询的等待间隔。
    /// </summary>
    /// <returns>配置允许的下一次轮询等待时间。</returns>
    private TimeSpan GetDrainWaitInterval()
    {
        if (_options.DeadLetterRetention <= TimeSpan.Zero)
            return _options.OutboxPollInterval;
        return _options.DeadLetterCleanupInterval < _options.OutboxPollInterval
            ? _options.DeadLetterCleanupInterval
            : _options.OutboxPollInterval;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (_stop == null)
            return;

        _stop.Cancel();
        try { _signal.Release(); }
        catch (ObjectDisposedException) { }
        try { _worker.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { }
        _outbox.Dispose();
        _signal.Dispose();
        _stop.Dispose();
    }

    /// <summary>
    /// 检查当前投递器是否已经释放。
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(HybridInvalidationDelivery));
    }

    /// <summary>
    /// 获取支持人工管理操作的 dead-letter outbox。
    /// </summary>
    /// <returns>dead-letter 管理 outbox。</returns>
    private IHybridInvalidationDeadLetterAdminOutbox GetDeadLetterAdminOutbox()
    {
        if (_outbox == null)
            throw new NotSupportedException("当前 Hybrid 实例未启用 Redis outbox。 ");
        return _outbox as IHybridInvalidationDeadLetterAdminOutbox ??
            throw new NotSupportedException("当前 outbox 不支持 dead-letter 人工管理。 ");
    }

    /// <summary>
    /// 授权 dead-letter 管理请求并记录授权失败。
    /// </summary>
    /// <param name="request">待授权的管理请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task AuthorizeAsync(
        HybridInvalidationDeadLetterAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (_deadLetterAuthorizer == null || _deadLetterAuditSink == null)
            throw new InvalidOperationException(
                "Hybrid dead-letter 管理必须同时配置授权器和审计接收器。 ");
        try
        {
            await _deadLetterAuthorizer.AuthorizeAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            var failureReason = error.Message;
            if (string.IsNullOrEmpty(failureReason))
                failureReason = error.GetType().Name;
            try
            {
                await _deadLetterAuditSink.RecordAsync(new HybridInvalidationDeadLetterAuditEntry(
                    request.Action, request.Topic, request.Actor, request.MessageId, false, 0,
                    DateTimeOffset.UtcNow, failureReason), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception auditError)
            {
                throw new AggregateException("dead-letter 授权拒绝且审计失败。", error, auditError);
            }
            throw;
        }
    }

    /// <summary>
    /// 将 dead-letter 管理结果写入审计接收器。
    /// </summary>
    /// <param name="entry">审计记录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private Task AuditAsync(HybridInvalidationDeadLetterAuditEntry entry,
        CancellationToken cancellationToken) =>
        _deadLetterAuditSink.RecordAsync(entry, cancellationToken);

    /// <summary>
    /// 记录 dead-letter 管理操作失败。
    /// </summary>
    /// <param name="request">失败的管理请求。</param>
    /// <param name="error">操作异常。</param>
    private async Task RecordAdminFailureAsync(
        HybridInvalidationDeadLetterAuthorizationRequest request, Exception error)
    {
        var reason = error?.Message;
        if (string.IsNullOrEmpty(reason))
            reason = error?.GetType().Name;
        try
        {
            await AuditAsync(new HybridInvalidationDeadLetterAuditEntry(
                request.Action, request.Topic, request.Actor, request.MessageId, false, 0,
                DateTimeOffset.UtcNow, reason), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception auditError)
        {
            throw new AggregateException("dead-letter 管理失败且审计失败。", error, auditError);
        }
    }

    /// <summary>
    /// 验证 dead-letter 管理操作的操作者标识。
    /// </summary>
    /// <param name="actor">操作者标识。</param>
    private static void ValidateActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("dead-letter 管理操作必须提供 actor。", nameof(actor));
    }

    /// <summary>
    /// 验证管理接口的单次消息数量上限。
    /// </summary>
    /// <param name="maximumMessages">请求的最大消息数。</param>
    private static void ValidateAdminBatchSize(int maximumMessages)
    {
        if (maximumMessages <= 0 || maximumMessages > 1_000)
            throw new ArgumentOutOfRangeException(nameof(maximumMessages));
    }

    /// <summary>
    /// 验证 dead-letter 管理句柄格式。
    /// </summary>
    /// <param name="handle">dead-letter 句柄。</param>
    private static void ValidateHandle(string handle)
    {
        if (!HybridInvalidationDeadLetterHandle.IsValid(handle))
            throw new ArgumentException("Hybrid dead-letter handle 格式无效。", nameof(handle));
    }

    /// <summary>
    /// 验证 dead-letter 管理条目及其确认凭据。
    /// </summary>
    /// <param name="entry">待校验的管理条目。</param>
    private static void ValidateEntry(HybridInvalidationDeadLetterEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        if (!HybridInvalidationDeadLetterHandle.IsValid(entry.Handle) ||
            string.IsNullOrWhiteSpace(entry.ConfirmationToken))
            throw new ArgumentException("dead-letter 条目缺少管理 token。", nameof(entry));
    }

    /// <summary>
    /// 将 outbox 记录转换为可审阅的 dead-letter 条目。
    /// </summary>
    /// <param name="record">持久化的 dead-letter 记录。</param>
    /// <returns>包含管理句柄和确认令牌的条目。</returns>
    private static HybridInvalidationDeadLetterEntry CreateDeadLetterEntry(
        HybridInvalidationDeadLetterRecord record)
    {
        var payload = record.Payload;
        var handle = record.Handle;
        var confirmationToken = HybridInvalidationDeadLetterHandle.CreateConfirmationToken(payload);
        try
        {
            var envelope = JsonSerializer.Deserialize<AdminEnvelope>(payload);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.Topic) ||
                string.IsNullOrWhiteSpace(envelope.Id) || envelope.CacheKeys == null)
                throw new InvalidOperationException();
            var cacheKeys = envelope.CacheKeys.ToArray();
            return new HybridInvalidationDeadLetterEntry(envelope.Topic, envelope.Id,
                Array.AsReadOnly(cacheKeys), envelope.IsPrefix, envelope.IsPattern,
                record.DeadLetteredAtUtc, Encoding.UTF8.GetByteCount(payload), false,
                handle, confirmationToken);
        }
        catch (JsonException)
        {
            return CreateMalformedDeadLetterEntry(record, handle, confirmationToken);
        }
        catch (InvalidOperationException)
        {
            return CreateMalformedDeadLetterEntry(record, handle, confirmationToken);
        }
    }

    /// <summary>
    /// 为无法解析的载荷创建损坏标记条目。
    /// </summary>
    /// <param name="record">持久化的 dead-letter 记录。</param>
    /// <param name="handle">记录管理句柄。</param>
    /// <param name="confirmationToken">删除确认令牌。</param>
    /// <returns>标记为损坏的 dead-letter 条目。</returns>
    private static HybridInvalidationDeadLetterEntry CreateMalformedDeadLetterEntry(
        HybridInvalidationDeadLetterRecord record, string handle, string confirmationToken) =>
        new HybridInvalidationDeadLetterEntry(null, null, Array.Empty<string>(), false, false,
            record.DeadLetteredAtUtc, Encoding.UTF8.GetByteCount(record.Payload), true,
            handle, confirmationToken);

    /// <summary>
    /// 校验 dead-letter 条目的 handle 和删除确认令牌。
    /// </summary>
    /// <param name="entry">待验证的 dead-letter 条目。</param>
    /// <param name="payload">原始序列化载荷。</param>
    /// <param name="confirmationToken">调用方提交的确认令牌。</param>
    private static void ValidateConfirmationToken(HybridInvalidationDeadLetterEntry entry,
        string payload, string confirmationToken)
    {
        var expectedHandle = HybridInvalidationDeadLetterHandle.Create(payload);
        var expectedToken = HybridInvalidationDeadLetterHandle.CreateConfirmationToken(payload);
        if (!string.Equals(entry.Handle, expectedHandle, StringComparison.Ordinal) ||
            !string.Equals(entry.ConfirmationToken, expectedToken, StringComparison.Ordinal) ||
            !string.Equals(confirmationToken, expectedToken, StringComparison.Ordinal))
            throw new ArgumentException("dead-letter 确认 token 与 handle 不匹配。",
                nameof(confirmationToken));
    }

    /// <summary>
    /// 表示从持久化失效消息载荷解析出的管理视图。
    /// </summary>
    private sealed class AdminEnvelope
    {
        /// <summary>
        /// 获取或设置失效消息主题。
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// 获取或设置失效消息标识。
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// 获取或设置待失效的物理缓存键。
        /// </summary>
        public string[] CacheKeys { get; set; }
        /// <summary>
        /// 获取或设置是否按键前缀执行失效。
        /// </summary>
        public bool IsPrefix { get; set; }
        /// <summary>
        /// 获取或设置是否按键模式执行失效。
        /// </summary>
        public bool IsPattern { get; set; }
    }

    /// <summary>
    /// 根据 outbox 状态生成投递诊断快照。
    /// </summary>
    /// <param name="status">可选的持久化 outbox 状态。</param>
    /// <returns>当前投递诊断快照。</returns>
    private HybridInvalidationDeliverySnapshot CreateSnapshot(HybridInvalidationOutboxStatus status)
    {
        DateTimeOffset? lastFailureUtc;
        string lastFailureMessage;
        lock (_diagnosticsGate)
        {
            lastFailureUtc = _lastFailureUtc;
            lastFailureMessage = _lastFailureMessage;
        }

        var now = DateTimeOffset.UtcNow;
        return new HybridInvalidationDeliverySnapshot(Topic, _outbox != null,
            status?.PendingMessages ?? 0, status?.ProcessingMessages ?? 0,
            status?.DeadLetterMessages ?? 0,
            status?.DeadLetterBytes,
            GetAge(now, status?.OldestPendingEnqueuedAtUtc),
            GetAge(now, status?.OldestProcessingEnqueuedAtUtc),
            GetAge(now, status?.OldestDeadLetteredAtUtc),
            Interlocked.Read(ref _successfulPublishCount),
            Interlocked.Read(ref _failedPublishAttemptCount),
            Interlocked.Read(ref _enqueuedMessageCount),
            Interlocked.Read(ref _deadLetteredMessageCount),
            Interlocked.Read(ref _expiredDeadLetterCount), lastFailureUtc,
            lastFailureMessage);
    }

    /// <summary>
    /// 判断失败消息是否达到移入 dead-letter 的投递次数。
    /// </summary>
    /// <param name="item">待判断的 outbox 消息。</param>
    /// <returns>达到已启用的 dead-letter 次数上限时返回 <see langword="true"/>。</returns>
    private bool ShouldDeadLetter(HybridInvalidationOutboxItem item) =>
        item != null && _options.MaxDeliveryAttempts > 0 &&
        _options.DeadLetterMaxMessages > 0 &&
        item.DeliveryAttempt >= _options.MaxDeliveryAttempts;

    /// <summary>
    /// 更新最近一次失败信息和失败尝试计数。
    /// </summary>
    /// <param name="error">失败异常。</param>
    /// <param name="countPublishAttempt">是否计入发布尝试失败数。</param>
    private void RecordFailure(Exception error, bool countPublishAttempt)
    {
        if (countPublishAttempt)
            Interlocked.Increment(ref _failedPublishAttemptCount);
        lock (_diagnosticsGate)
        {
            _lastFailureUtc = DateTimeOffset.UtcNow;
            var message = error?.Message;
            if (string.IsNullOrEmpty(message))
                message = error?.GetType().Name;
            _lastFailureMessage = message == null || message.Length <= 512
                ? message
                : message.Substring(0, 512);
        }
    }

    /// <summary>
    /// 计算消息时间戳相对当前时间的非负间隔。
    /// </summary>
    /// <param name="now">当前时间。</param>
    /// <param name="enqueuedAtUtc">消息时间戳。</param>
    /// <returns>消息年龄；没有时间戳时返回 <see langword="null"/>。</returns>
    private static TimeSpan? GetAge(DateTimeOffset now, DateTimeOffset? enqueuedAtUtc)
    {
        if (!enqueuedAtUtc.HasValue)
            return null;
        var age = now - enqueuedAtUtc.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }
}
