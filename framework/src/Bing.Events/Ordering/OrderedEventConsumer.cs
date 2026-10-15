using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Bing.Data.Events.Ordering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bing.Events.Ordering;

/// <summary>
/// 在当前事务内处理有序事件。
/// </summary>
public interface IOrderedEventHandler
{
    /// <summary>
    /// 执行业务修改。
    /// </summary>
    /// <remarks>必须使用传入事务，不得提前提交；外部副作用需要幂等键或事务出箱。</remarks>
    /// <param name="stream">待处理事件所属的业务流。</param>
    /// <param name="message">待处理的有序事件。</param>
    /// <param name="transaction">用于提交业务修改的数据库事务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message, DbTransaction transaction,
        CancellationToken cancellationToken);
}

/// <summary>
/// 注册显式启用的有序消费者。
/// </summary>
public static class OrderedEventConsumerExtensions
{
    /// <summary>
    /// 默认连续扫描页数上限。
    /// </summary>
    private const int DefaultMaxPagesPerScan = 16;

    /// <summary>
    /// 注册有序消费者。
    /// </summary>
    /// <typeparam name="THandler">事务内业务处理器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="consumer">跨蓝绿版本稳定的逻辑消费者名称。</param>
    /// <param name="pollInterval">空闲或阻塞流的扫描间隔。</param>
    /// <param name="batchSize">单轮读取业务流的数量。</param>
    /// <param name="maxConcurrency">同一消费者同时处理的业务流上限；默认值为 1。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>调用方先注册当前作用域的 IOrderedEventTransactionFactory 和 IOrderedEventStore。</remarks>
    public static IServiceCollection AddBingOrderedConsumer<THandler>(this IServiceCollection services,
        string consumer, TimeSpan? pollInterval = null, int batchSize = 100, int maxConcurrency = 1)
        where THandler : class, IOrderedEventHandler
    {
        return AddBingOrderedConsumer<THandler>(services, consumer, pollInterval, batchSize, maxConcurrency,
            DefaultMaxPagesPerScan);
    }

    /// <summary>
    /// 注册有序消费者并设置连续扫描页数上限。
    /// </summary>
    /// <typeparam name="THandler">事务内业务处理器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="consumer">跨蓝绿版本稳定的逻辑消费者名称。</param>
    /// <param name="maxPagesPerScan">同一轮连续分页的最大页数。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddBingOrderedConsumer<THandler>(this IServiceCollection services,
        string consumer, int maxPagesPerScan)
        where THandler : class, IOrderedEventHandler =>
        AddBingOrderedConsumer<THandler>(services, consumer, null, 100, 1, maxPagesPerScan);

    /// <summary>
    /// 注册有序消费者并设置并发流数和连续扫描页数上限。
    /// </summary>
    /// <typeparam name="THandler">事务内业务处理器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="consumer">跨蓝绿版本稳定的逻辑消费者名称。</param>
    /// <param name="maxConcurrency">同一消费者同时处理的业务流上限。</param>
    /// <param name="maxPagesPerScan">同一轮连续分页的最大页数。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddBingOrderedConsumer<THandler>(this IServiceCollection services,
        string consumer, int maxConcurrency, int maxPagesPerScan)
        where THandler : class, IOrderedEventHandler =>
        AddBingOrderedConsumer<THandler>(services, consumer, null, 100, maxConcurrency, maxPagesPerScan);

    /// <summary>
    /// 注册有序消费者。
    /// </summary>
    /// <typeparam name="THandler">事务内业务处理器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="consumer">跨蓝绿版本稳定的逻辑消费者名称。</param>
    /// <param name="pollInterval">空闲或阻塞流的扫描间隔。</param>
    /// <param name="batchSize">单轮读取业务流的数量。</param>
    /// <param name="maxConcurrency">同一消费者同时处理的业务流上限；默认值为 1。</param>
    /// <param name="maxPagesPerScan">同一轮连续分页的最大页数；达到上限后回到首页，避免新流长期等待。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>调用方先注册当前作用域的 IOrderedEventTransactionFactory 和 IOrderedEventStore。</remarks>
    public static IServiceCollection AddBingOrderedConsumer<THandler>(this IServiceCollection services,
        string consumer, TimeSpan? pollInterval, int batchSize, int maxConcurrency, int maxPagesPerScan)
        where THandler : class, IOrderedEventHandler
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        var interval = pollInterval ?? TimeSpan.FromSeconds(1);
        if (interval <= TimeSpan.Zero || interval.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        if (batchSize <= 0 || batchSize > 1000)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (maxConcurrency <= 0 || maxConcurrency > 100)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        if (maxPagesPerScan <= 0 || maxPagesPerScan > 1000)
            throw new ArgumentOutOfRangeException(nameof(maxPagesPerScan));
        if (services.Any(item => item.ServiceType == typeof(ConsumerRegistration) &&
                item.ImplementationInstance is ConsumerRegistration existing && existing.Name == consumer))
            throw new InvalidOperationException("同一服务集合不能重复注册逻辑消费者。");
        var metrics = new BingOrderedEventMetrics(consumer);
        var registration = new ConsumerRegistration(consumer, interval, batchSize, maxConcurrency, maxPagesPerScan,
            metrics);
        services.AddSingleton(registration);
        services.AddSingleton(metrics);
        services.AddSingleton<IOrderedEventMetrics>(metrics);
        services.TryAddScoped<OrderedEventCoordinator>();
        services.TryAddScoped<OrderedEventPublisher>();
        services.AddBingCapOrderedEventIngress();
        services.TryAddScoped<THandler>();
        services.AddSingleton<ConsumerWorker<THandler>>(provider => new ConsumerWorker<THandler>(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<ConsumerWorker<THandler>>>(), registration));
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ConsumerWorker<THandler>>());
        services.AddSingleton<IOrderedEventConsumerRuntime>(provider =>
            provider.GetRequiredService<ConsumerWorker<THandler>>());
        return services;
    }

    /// <summary>
    /// 宿主所属的消费者配置。
    /// </summary>
    private sealed class ConsumerRegistration
    {
        /// <summary>
        /// 初始化一个 <see cref="ConsumerRegistration" /> 类型的实例。
        /// </summary>
        /// <param name="name">逻辑消费者名称。</param>
        /// <param name="interval">轮询间隔。</param>
        /// <param name="batchSize">单轮扫描的业务流数量。</param>
        /// <param name="maxConcurrency">同一消费者同时处理的业务流上限。</param>
        /// <param name="maxPagesPerScan">同一轮连续分页的最大页数。</param>
        /// <param name="metrics">消费者运行指标。</param>
        internal ConsumerRegistration(string name, TimeSpan interval, int batchSize, int maxConcurrency,
            int maxPagesPerScan, BingOrderedEventMetrics metrics)
        {
            Name = name;
            Interval = interval;
            BatchSize = batchSize;
            MaxConcurrency = maxConcurrency;
            MaxPagesPerScan = maxPagesPerScan;
            Metrics = metrics;
        }
        /// <summary>
        /// 获取逻辑消费者名称。
        /// </summary>
        internal string Name { get; }
        /// <summary>
        /// 获取轮询间隔。
        /// </summary>
        internal TimeSpan Interval { get; }
        /// <summary>
        /// 获取单轮扫描的业务流数量。
        /// </summary>
        internal int BatchSize { get; }
        /// <summary>
        /// 获取同一消费者的并发流上限。
        /// </summary>
        internal int MaxConcurrency { get; }
        /// <summary>
        /// 获取同一轮连续分页的最大页数。
        /// </summary>
        internal int MaxPagesPerScan { get; }
        /// <summary>
        /// 获取消费者运行指标。
        /// </summary>
        internal BingOrderedEventMetrics Metrics { get; }
    }

    /// <summary>
    /// 逐实体使用独立作用域处理持久化收件箱。
    /// </summary>
    /// <typeparam name="THandler">事务内业务处理器类型。</typeparam>
    private sealed class ConsumerWorker<THandler> : BackgroundService, IOrderedEventConsumerRuntime
        where THandler : class, IOrderedEventHandler
    {
        /// <summary>
        /// 失败流状态探测缓存的最大条目数。
        /// </summary>
        private const int MaxFailedStreamProbes = 4096;

        /// <summary>
        /// 缺口告警缓存的最大条目数。
        /// </summary>
        private const int MaxGapWarnings = 4096;

        /// <summary>
        /// 同一业务流缺口告警的节流时长。
        /// </summary>
        private const long GapWarningThrottleMilliseconds = 30_000L;

        /// <summary>
        /// 缺口告警缓存满载后的全局节流时长。
        /// </summary>
        private const long GapWarningOverflowThrottleMilliseconds = 1_000L;

        /// <summary>
        /// 宿主作用域工厂。
        /// </summary>
        private readonly IServiceScopeFactory _scopes;
        /// <summary>
        /// 安全诊断日志。
        /// </summary>
        private readonly ILogger _logger;
        /// <summary>
        /// 消费者配置。
        /// </summary>
        private readonly ConsumerRegistration _registration;
        /// <summary>
        /// 记录扫描循环是否已经实际退出。
        /// </summary>
        private readonly TaskCompletionSource<bool> _executionCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 指示后台扫描循环是否已经启动。
        /// </summary>
        private int _executionStarted;
        /// <summary>
        /// 指示当前是否已经请求停止继续扫描。
        /// </summary>
        private int _draining;

        /// <summary>
        /// 保存失败流下一次状态探测的单调时钟时间。
        /// </summary>
        private readonly ConcurrentDictionary<string, long> _failedStreamProbes =
            new(StringComparer.Ordinal);

        /// <summary>
        /// 保护失败探测缓存的容量变更。
        /// </summary>
        private readonly object _failedStreamProbeGate = new object();

        /// <summary>
        /// 失败缓存达到上限时的全局退避截止时间。
        /// </summary>
        private long _failedStreamOverflowProbeUntil;

        /// <summary>
        /// 保存缺口告警下一次允许记录的单调时钟时间。
        /// </summary>
        private readonly ConcurrentDictionary<string, long> _gapWarningUntil =
            new(StringComparer.Ordinal);

        /// <summary>
        /// 保护缺口告警缓存的容量变更。
        /// </summary>
        private readonly object _gapWarningGate = new object();

        /// <summary>
        /// 缺口告警缓存满载后的全局节流截止时间。
        /// </summary>
        private long _gapWarningOverflowUntil;

        /// <summary>
        /// 初始化一个 <see cref="ConsumerWorker{THandler}" /> 类型的实例。
        /// </summary>
        /// <param name="scopes">创建处理作用域的工厂。</param>
        /// <param name="logger">记录安全诊断信息的日志。</param>
        /// <param name="registration">消费者配置。</param>
        internal ConsumerWorker(IServiceScopeFactory scopes, ILogger logger, ConsumerRegistration registration)
        {
            _scopes = scopes;
            _logger = logger;
            _registration = registration;
        }

        /// <inheritdoc />
        public string ConsumerName => _registration.Name;

        /// <inheritdoc />
        public bool IsRunning => Volatile.Read(ref _executionStarted) != 0 &&
            !_executionCompleted.Task.IsCompleted;

        /// <inheritdoc />
        public bool IsDraining => Volatile.Read(ref _draining) != 0;

        /// <inheritdoc />
        public long InFlightCount => _registration.Metrics.Snapshot.InFlightCount;

        /// <inheritdoc />
        public void BeginDrain() => Interlocked.Exchange(ref _draining, 1);

        /// <inheritdoc />
        public async Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (timeout < Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (Volatile.Read(ref _executionStarted) == 0 || _executionCompleted.Task.IsCompleted)
                return true;

            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completed = await Task.WhenAny(_executionCompleted.Task,
                timeout == Timeout.InfiniteTimeSpan
                    ? Task.Delay(Timeout.Infinite, waitCancellation.Token)
                    : Task.Delay(timeout, waitCancellation.Token)).ConfigureAwait(false);
            waitCancellation.Cancel();
            return ReferenceEquals(completed, _executionCompleted.Task) && _executionCompleted.Task.IsCompletedSuccessfully;
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            BeginDrain();
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
            if (Volatile.Read(ref _executionStarted) == 0 || _executionCompleted.Task.IsCompleted)
                return;

            var snapshot = _registration.Metrics.Snapshot;
            const string message = "有序消费者停止排空未完成，后台扫描或处理任务尚未退出。";
            _logger.LogWarning("{Message} Consumer={Consumer}, InFlight={InFlight}, WaitCancelled={WaitCancelled}",
                message, _registration.Name, snapshot.InFlightCount, cancellationToken.IsCancellationRequested);
            Exception exception = cancellationToken.IsCancellationRequested
                ? new OperationCanceledException(message, cancellationToken)
                : new TimeoutException(message);
            exception.Data["Bing.OrderedEventConsumer"] = _registration.Name;
            exception.Data["Bing.OrderedEventConsumerInFlight"] = snapshot.InFlightCount;
            exception.Data["Bing.OrderedEventConsumerDrainIncomplete"] = true;
            throw exception;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Volatile.Write(ref _executionStarted, 1);
            try
            {
                string after = null;
                var pagesSinceReset = 0;
                while (!stoppingToken.IsCancellationRequested && !IsDraining)
                {
                    try
                    {
                        IReadOnlyList<OrderedEventStream> streams;
                        long? pendingStreams = null;
                        long? pendingEvents = null;
                        var pendingStreamsScanSucceeded = true;
                        TimeSpan? oldestPendingEventAge = null;
                        var pendingEventsScanSucceeded = true;
                        var oldestPendingEventAgeScanSucceeded = true;
                        await using (var scope = _scopes.CreateAsyncScope())
                        {
                            var coordinator = scope.ServiceProvider.GetRequiredService<OrderedEventCoordinator>();
                            streams = await coordinator.GetPendingStreamsAsync(_registration.Name, _registration.BatchSize,
                                after, stoppingToken);
                            try
                            {
                                pendingStreams = await coordinator.GetPendingStreamCountAsync(_registration.Name,
                                    stoppingToken);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                pendingStreamsScanSucceeded = false;
                                _registration.Metrics.RecordScanFailure();
                                _logger.LogWarning("有序消费待处理流统计失败，将保留上一份统计快照。Consumer={Consumer}, ErrorType={ErrorType}",
                                    _registration.Name, exception.GetType().Name);
                            }
                            try
                            {
                                pendingEvents = await coordinator.GetPendingEventCountAsync(_registration.Name, stoppingToken);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                pendingEventsScanSucceeded = false;
                                _registration.Metrics.RecordScanFailure();
                                _logger.LogWarning("有序消费积压统计失败，将保留上一份统计快照。Consumer={Consumer}, ErrorType={ErrorType}",
                                    _registration.Name, exception.GetType().Name);
                            }
                            try
                            {
                                oldestPendingEventAge = await coordinator.GetOldestPendingEventAgeAsync(
                                    _registration.Name, stoppingToken);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                oldestPendingEventAgeScanSucceeded = false;
                                _registration.Metrics.RecordScanFailure();
                                _logger.LogWarning("有序消费最老积压统计失败。Consumer={Consumer}, ErrorType={ErrorType}",
                                    _registration.Name, exception.GetType().Name);
                            }
                        }
                        if (pendingStreamsScanSucceeded)
                            _registration.Metrics.RecordPendingStreams(pendingStreams ?? streams.Count);
                        if (pendingEventsScanSucceeded)
                            _registration.Metrics.RecordPendingEvents(pendingEvents ?? streams.Count);
                        if (oldestPendingEventAgeScanSucceeded)
                            _registration.Metrics.RecordOldestPendingEventAge(oldestPendingEventAge);
                        // 同一流仍由数据库游标锁保证串行；不同流按有界槽位并行，完成一个流后立即补位。
                        // 页面全部完成后仍按扫描顺序推进游标，避免异步完成顺序造成跳过。
                        if (streams.Count > 0)
                        {
                            after = await ProcessStreamsAsync(streams, stoppingToken).ConfigureAwait(false);
                            pagesSinceReset++;
                        }
                        if (IsDraining)
                            break;
                        if (streams.Count < _registration.BatchSize ||
                            pagesSinceReset >= _registration.MaxPagesPerScan)
                        {
                            after = null;
                            pagesSinceReset = 0;
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        _registration.Metrics.RecordScanFailure();
                        _logger.LogError("有序消费扫描失败。Consumer={Consumer}, ErrorType={ErrorType}",
                            _registration.Name, exception.GetType().Name);
                        after = null;
                        pagesSinceReset = 0;
                    }
                    try
                    {
                        await Task.Delay(_registration.Interval, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            finally
            {
                _executionCompleted.TrySetResult(true);
            }
        }

        /// <summary>
        /// 使用有界槽位处理当前扫描页面。
        /// </summary>
        /// <param name="streams">当前扫描页面中的业务流。</param>
        /// <param name="stoppingToken">宿主停止令牌。</param>
        /// <returns>页面最后一个业务流的稳定分页键。</returns>
        private async Task<string> ProcessStreamsAsync(IReadOnlyList<OrderedEventStream> streams,
            CancellationToken stoppingToken)
        {
            var pending = new List<(int Index, Task<string> Task)>();
            var completedKeys = new string[streams.Count];
            ExceptionDispatchInfo failure = null;
            var nextIndex = 0;
            var stopScheduling = false;

            while (nextIndex < streams.Count || pending.Count > 0)
            {
                while (!stopScheduling && !stoppingToken.IsCancellationRequested &&
                       nextIndex < streams.Count && pending.Count < _registration.MaxConcurrency)
                {
                    var index = nextIndex++;
                    pending.Add((index, ProcessStreamAsync(streams[index], stoppingToken)));
                }

                if (pending.Count == 0)
                    break;

                var tasks = pending.Select(item => item.Task).ToArray();
                var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                var completedIndex = pending.FindIndex(item => ReferenceEquals(item.Task, completedTask));
                var completed = pending[completedIndex];
                pending.RemoveAt(completedIndex);
                try
                {
                    completedKeys[completed.Index] = await completed.Task.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                    stopScheduling = true;
                }
            }

            if (stoppingToken.IsCancellationRequested)
                stoppingToken.ThrowIfCancellationRequested();
            failure?.Throw();
            return completedKeys[^1];
        }

        /// <summary>
        /// 在独立作用域中处理一个业务流。
        /// </summary>
        /// <param name="stream">待处理的业务流。</param>
        /// <param name="stoppingToken">宿主停止令牌。</param>
        /// <returns>该业务流的稳定分页键。</returns>
        private async Task<string> ProcessStreamAsync(OrderedEventStream stream, CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();
            _registration.Metrics.RecordStarted();
            var started = Stopwatch.GetTimestamp();
            string streamKey = null;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IOrderedEventStore>();
                streamKey = store.GetStreamKey(stream);
                var coordinator = scope.ServiceProvider.GetRequiredService<OrderedEventCoordinator>();
                try
                {
                    if (IsFailedStreamDeferred(streamKey))
                        return streamKey;

                    if (IsFailedStreamProbeDue(streamKey) || IsFailedStreamOverflowProbeDue())
                    {
                        var state = await coordinator.GetStateAsync(_registration.Name, stream, stoppingToken)
                            .ConfigureAwait(false);
                        if (state.Failed)
                        {
                            DeferFailedStream(streamKey);
                            return streamKey;
                        }
                        _failedStreamProbes.TryRemove(streamKey, out _);
                        ScheduleNextOverflowProbeIfNeeded();
                    }

                    var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                    var result = await coordinator.ProcessNextAsync(_registration.Name, stream,
                        (message, transaction, token) => handler.HandleAsync(stream, message, transaction, token), stoppingToken);
                    var elapsed = GetElapsed(started);
                    if (result == OrderedEventProcessingResult.Processed)
                    {
                        _registration.Metrics.RecordProcessed(elapsed);
                        ClearGapWarning(streamKey);
                    }
                    else if (result == OrderedEventProcessingResult.Gap)
                    {
                        _registration.Metrics.RecordGap();
                        if (ShouldLogGap(streamKey))
                            _logger.LogWarning("有序消费被阻塞。Consumer={Consumer}, StreamKey={StreamKey}, State={State}",
                                _registration.Name, streamKey, result);
                    }
                    else if (result == OrderedEventProcessingResult.Failed)
                    {
                        _registration.Metrics.RecordFailure(elapsed);
                        ClearGapWarning(streamKey);
                        if (DeferFailedStream(streamKey))
                            _logger.LogWarning("有序消费被阻塞。Consumer={Consumer}, StreamKey={StreamKey}, State={State}",
                                _registration.Name, streamKey, result);
                    }
                    else
                    {
                        _failedStreamProbes.TryRemove(streamKey, out _);
                        ClearGapWarning(streamKey);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _registration.Metrics.RecordFailure(GetElapsed(started));
                    if (streamKey != null)
                    {
                        DeferFailedStream(streamKey);
                        ClearGapWarning(streamKey);
                    }
                    // 不输出可能包含消息内容的 Exception.Message。
                    _logger.LogError("有序消费失败。Consumer={Consumer}, StreamKey={StreamKey}, ErrorType={ErrorType}",
                        _registration.Name, streamKey, exception.GetType().Name);
                }
                return streamKey;
            }
            finally
            {
                _registration.Metrics.RecordCompleted();
            }
        }

        /// <summary>
        /// 判断失败流是否仍处于退避窗口。
        /// </summary>
        /// <param name="streamKey">业务流稳定键。</param>
        /// <returns>仍需退避时返回 <see langword="true"/>。</returns>
        private bool IsFailedStreamDeferred(string streamKey)
        {
            var now = Environment.TickCount64;
            if (_failedStreamProbes.TryGetValue(streamKey, out var nextProbe))
                return now < nextProbe;
            return now < Volatile.Read(ref _failedStreamOverflowProbeUntil);
        }

        /// <summary>
        /// 判断失败流是否应重新读取持久化状态。
        /// </summary>
        /// <param name="streamKey">业务流稳定键。</param>
        /// <returns>已有缓存且已到探测时间时返回 <see langword="true"/>。</returns>
        private bool IsFailedStreamProbeDue(string streamKey)
        {
            return _failedStreamProbes.TryGetValue(streamKey, out var nextProbe) &&
                Environment.TickCount64 >= nextProbe;
        }

        /// <summary>
        /// 判断失败缓存满载后的全局状态探测是否到期。
        /// </summary>
        /// <returns>缓存满载且全局探测已到期时返回 <see langword="true"/>。</returns>
        private bool IsFailedStreamOverflowProbeDue()
        {
            if (_failedStreamProbes.Count < MaxFailedStreamProbes)
                return false;
            var nextProbe = Volatile.Read(ref _failedStreamOverflowProbeUntil);
            return nextProbe != 0 && Environment.TickCount64 >= nextProbe;
        }

        /// <summary>
        /// 判断是否应记录业务流缺口告警。
        /// </summary>
        /// <param name="streamKey">业务流稳定键。</param>
        /// <returns>允许记录时返回 <see langword="true" />。</returns>
        private bool ShouldLogGap(string streamKey)
        {
            if (string.IsNullOrEmpty(streamKey))
                return false;

            lock (_gapWarningGate)
            {
                var now = Environment.TickCount64;
                if (_gapWarningUntil.TryGetValue(streamKey, out var nextWarning))
                {
                    if (now < nextWarning)
                        return false;
                    _gapWarningUntil[streamKey] = now + GapWarningThrottleMilliseconds;
                    return true;
                }

                if (_gapWarningUntil.Count >= MaxGapWarnings)
                {
                    foreach (var item in _gapWarningUntil)
                    {
                        if (item.Value <= now)
                            _gapWarningUntil.TryRemove(item.Key, out _);
                        if (_gapWarningUntil.Count < MaxGapWarnings)
                            break;
                    }

                    if (_gapWarningUntil.Count >= MaxGapWarnings)
                    {
                        var overflowUntil = Volatile.Read(ref _gapWarningOverflowUntil);
                        if (now < overflowUntil)
                            return false;
                        Interlocked.Exchange(ref _gapWarningOverflowUntil,
                            now + GapWarningOverflowThrottleMilliseconds);
                        return true;
                    }
                }

                _gapWarningUntil[streamKey] = now + GapWarningThrottleMilliseconds;
                if (_gapWarningUntil.Count >= MaxGapWarnings)
                    Interlocked.Exchange(ref _gapWarningOverflowUntil,
                        now + GapWarningOverflowThrottleMilliseconds);
                return true;
            }
        }

        /// <summary>
        /// 清除已恢复业务流的缺口告警状态。
        /// </summary>
        /// <param name="streamKey">业务流稳定键。</param>
        private void ClearGapWarning(string streamKey)
        {
            if (string.IsNullOrEmpty(streamKey))
                return;
            lock (_gapWarningGate)
            {
                _gapWarningUntil.TryRemove(streamKey, out _);
                if (_gapWarningUntil.Count < MaxGapWarnings)
                    Interlocked.Exchange(ref _gapWarningOverflowUntil, 0L);
            }
        }

        /// <summary>
        /// 在失败缓存仍满载时安排下一次全局状态探测。
        /// </summary>
        private void ScheduleNextOverflowProbeIfNeeded()
        {
            if (_failedStreamProbes.Count >= MaxFailedStreamProbes)
                Interlocked.Exchange(ref _failedStreamOverflowProbeUntil, Environment.TickCount64 + 100L);
            else
                Interlocked.Exchange(ref _failedStreamOverflowProbeUntil, 0L);
        }

        /// <summary>
        /// 记录失败流的下一次状态探测时间，并限制缓存规模。
        /// </summary>
        /// <param name="streamKey">业务流稳定键。</param>
        /// <returns>本次首次登记失败流时返回 <see langword="true"/>。</returns>
        private bool DeferFailedStream(string streamKey)
        {
            if (string.IsNullOrEmpty(streamKey))
                return false;
            lock (_failedStreamProbeGate)
            {
                var now = Environment.TickCount64;
                if (!_failedStreamProbes.ContainsKey(streamKey) &&
                    _failedStreamProbes.Count >= MaxFailedStreamProbes)
                {
                    foreach (var item in _failedStreamProbes)
                    {
                        if (item.Value > now)
                            continue;
                        _failedStreamProbes.TryRemove(item.Key, out _);
                        if (_failedStreamProbes.Count < MaxFailedStreamProbes)
                            break;
                    }
                    if (_failedStreamProbes.Count >= MaxFailedStreamProbes)
                    {
                        Interlocked.Exchange(ref _failedStreamOverflowProbeUntil, now + 100L);
                        return false;
                    }
                }

                var first = _failedStreamProbes.TryAdd(streamKey, NextFailedStreamProbe(now));
                if (!first)
                    _failedStreamProbes[streamKey] = NextFailedStreamProbe(now);
                if (_failedStreamProbes.Count >= MaxFailedStreamProbes)
                    Interlocked.Exchange(ref _failedStreamOverflowProbeUntil, now + 100L);
                return first;
            }
        }

        /// <summary>
        /// 计算失败流的退避时长。
        /// </summary>
        /// <param name="now">当前单调时钟时间。</param>
        /// <returns>下一次状态探测的单调时钟时间。</returns>
        private long NextFailedStreamProbe(long now)
        {
            var milliseconds = (long)_registration.Interval.TotalMilliseconds;
            if (milliseconds < 100L)
                milliseconds = 100L;
            else if (milliseconds > 5000L)
                milliseconds = 5000L;
            return now + milliseconds;
        }

        /// <summary>
        /// 计算单调时钟经过的时间。
        /// </summary>
        /// <param name="started">处理开始时的单调时钟时间戳。</param>
        /// <returns>从开始时间戳到当前时刻经过的非负时长。</returns>
        private static TimeSpan GetElapsed(long started) =>
            TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency);
    }
}
