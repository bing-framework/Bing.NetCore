using System.Data.Common;
using System.Runtime.ExceptionServices;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Bing.Events.Cap;

namespace Bing.Events.Ordering;

/// <summary>
/// 协调有序事件的接收、业务事务和失败恢复。
/// </summary>
public sealed class OrderedEventCoordinator
{
    /// <summary>
    /// 当前作用域的事务工厂。
    /// </summary>
    private readonly IOrderedEventTransactionFactory _transactions;
    /// <summary>
    /// 有序事件存储。
    /// </summary>
    private readonly IOrderedEventStore _store;
    /// <summary>
    /// 按逻辑消费者分组的运行指标。
    /// </summary>
    private readonly IReadOnlyList<BingOrderedEventMetrics> _metrics;
    /// <summary>
    /// 人工恢复授权器。
    /// </summary>
    private readonly IReadOnlyList<IOrderedEventRecoveryAuthorizer> _authorizers;

    /// <summary>
    /// 初始化一个 <see cref="OrderedEventCoordinator" /> 类型的实例。
    /// </summary>
    /// <param name="transactions">当前作用域的有序事件事务工厂。</param>
    /// <param name="store">持久化有序事件和游标的存储。</param>
    /// <param name="metrics">按消费者收集运行指标的实例集合。</param>
    /// <param name="authorizers">执行人工恢复授权检查的实例集合。</param>
    public OrderedEventCoordinator(IOrderedEventTransactionFactory transactions, IOrderedEventStore store,
        IEnumerable<BingOrderedEventMetrics> metrics = null,
        IEnumerable<IOrderedEventRecoveryAuthorizer> authorizers = null)
    {
        _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _metrics = metrics?.ToArray() ?? Array.Empty<BingOrderedEventMetrics>();
        _authorizers = authorizers?.ToArray() ?? Array.Empty<IOrderedEventRecoveryAuthorizer>();
    }

    /// <summary>
    /// 持久化带消息头的有序事件。
    /// </summary>
    /// <remarks>订阅方法必须等待此调用完成后返回，不能先确认消息；无需在接收线程执行业务。</remarks>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="headers">包含租户、业务流、实体和序号的事件头。</param>
    /// <param name="payload">事件负载。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task ReceiveAsync(string consumer, IReadOnlyDictionary<string, string> headers, string payload,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        var metadata = ClusterEventHeaders.Read(headers);
        if (metadata?.Sequence == null)
            throw new ArgumentException("接收的有序消息缺少连续序号及业务流元数据。", nameof(headers));
        return ReceiveAsync(consumer, new OrderedEventStream(metadata.TenantId, metadata.Stream, metadata.EntityKey),
            new OrderedEventMessage(metadata.EventId, metadata.Sequence.Value, payload, metadata.ContractVersion), cancellationToken);
    }

    /// <summary>
    /// 持久化有序事件。
    /// </summary>
    /// <remarks>内置存储发生原生 MySQL 死锁且确认回滚、清理成功时，最多重试三次整个入箱事务；业务处理和未知提交不自动重试。</remarks>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">事件所属的业务流。</param>
    /// <param name="message">待持久化的有序事件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task ReceiveAsync(string consumer, OrderedEventStream stream, OrderedEventMessage message,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await RunAsync(async transaction =>
                {
                    await _store.EnqueueAsync(transaction, consumer, stream, message, cancellationToken);
                    return true;
                }, cancellationToken);
                return;
            }
            catch (DbException exception) when (attempt < 3 && _store is DbOrderedEventStore &&
                                                IsRetryableReceiveFailure(exception))
            {
                // 只重做无业务回调的入箱事务；每次回滚与资源释放已完成，再等待下一次尝试。
                await Task.Delay(20 << attempt, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 识别已安全回滚的原生 MySQL 入箱死锁。
    /// </summary>
    /// <param name="exception">待判断的数据库异常。</param>
    /// <returns>异常表示可安全重试的 MySQL 死锁时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool IsRetryableReceiveFailure(DbException exception)
    {
        if (exception.GetTransactionOutcome() != TransactionOutcome.RolledBack ||
            exception.Data.Contains("Bing.TransactionCleanupException"))
            return false;
        var type = exception.GetType();
        if (type.FullName != "MySqlConnector.MySqlException" &&
            type.FullName != "MySql.Data.MySqlClient.MySqlException")
            return false;
        // 适配包没有引入数据库驱动引用；方法元数据只在当前调用中使用。
        return type.GetProperty("Number")?.GetValue(exception) is int number && number == 1213;
    }

    /// <summary>
    /// 处理下一事件并提交业务及游标。
    /// </summary>
    /// <remarks>
    /// 业务失败时先回滚保存点，再在仍持有游标锁的事务内记录失败。
    /// 原异常的 TransactionOutcome 表示外层事务结果；仅提交失败标记时另附 BusinessRolledBack 和 FailureRecorded 标识。
    /// </remarks>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">待处理事件所属的业务流。</param>
    /// <param name="handler">在当前数据库事务内执行业务修改的处理器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前事件的处理结果。</returns>
    public async Task<OrderedEventProcessingResult> ProcessNextAsync(string consumer, OrderedEventStream stream,
        Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        return await RunTransactionAsync(owner => _store.ProcessNextAsync(owner.Transaction, consumer, stream,
            (message, currentTransaction, token) => ExecuteBusinessAsync(consumer, stream, message,
                currentTransaction, owner as IOrderedEventTransactionState, handler, token), cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 隔离业务修改并在同一事务中记录失败。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">待处理事件所属的业务流。</param>
    /// <param name="message">当前处理的有序事件。</param>
    /// <param name="transaction">执行业务和失败记录的数据库事务。</param>
    /// <param name="state">负责清理事务外业务状态的可选事务适配器。</param>
    /// <param name="handler">执行业务修改的处理器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task ExecuteBusinessAsync(string consumer, OrderedEventStream stream, OrderedEventMessage message,
        DbTransaction transaction, IOrderedEventTransactionState state,
        Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        // 名称只由框架生成，避免覆盖业务回调自身的保存点。
        var savepoint = "bing_ordered_" + Guid.NewGuid().ToString("N");
        await ApplySavepointAsync(transaction, "SAVEPOINT " + savepoint,
            () => transaction.SaveAsync(savepoint, cancellationToken), cancellationToken);
        try
        {
            await handler(message, transaction, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 同一个异常对象可能被业务重复抛出，诊断标识只对应本次尝试。
            exception.Data.Remove("Bing.OrderedEventBusinessRolledBack");
            exception.Data.Remove("Bing.OrderedEventFailureRecorded");
            exception.Data.Remove("Bing.OrderedEventFailureRecordingException");
            try
            {
                // 游标锁在保存点之前取得，回滚业务不开放其他节点的处理窗口。
                await ApplySavepointAsync(transaction, "ROLLBACK TO SAVEPOINT " + savepoint,
                    () => transaction.RollbackAsync(savepoint, CancellationToken.None), CancellationToken.None);
                exception.Data["Bing.OrderedEventBusinessRolledBack"] = true;
                if (state != null)
                {
                    await state.ResetBusinessStateAsync(CancellationToken.None);
                    state.ActivateAmbient();
                }
                await _store.RecordFailureAsync(transaction, consumer, stream, message.Sequence,
                    exception.GetType().Name, CancellationToken.None);
                var persistedState = await _store.GetStateAsync(transaction, consumer, stream, CancellationToken.None);
                if (!persistedState.Failed || persistedState.FirstPendingSequence != message.Sequence)
                    throw new InvalidOperationException("当前事务未记录预期的有序事件失败状态。");
                await ApplySavepointAsync(transaction, "RELEASE SAVEPOINT " + savepoint,
                    () => transaction.ReleaseAsync(savepoint, CancellationToken.None), CancellationToken.None);
            }
            catch (Exception recordingException)
            {
                exception.Data["Bing.OrderedEventFailureRecordingException"] = recordingException;
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
            // 仅框架内部使用此信号；外层提交失败标记后仍向调用方传播同一个业务异常。
            throw new BusinessFailure(exception);
        }
        await ApplySavepointAsync(transaction, "RELEASE SAVEPOINT " + savepoint,
            () => transaction.ReleaseAsync(savepoint, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 执行当前事务的保存点操作。
    /// </summary>
    /// <remarks>旧 SQLite 驱动没有实现保存点 API；内置 MySQL/SQLite 存储使用同等 SQL，不回落到完整业务事务之外的失败记录。</remarks>
    /// <param name="transaction">当前数据库事务。</param>
    /// <param name="sql">不支持原生保存点时执行的保存点 SQL。</param>
    /// <param name="nativeOperation">数据库事务支持保存点时执行的原生操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task ApplySavepointAsync(DbTransaction transaction, string sql, Func<Task> nativeOperation,
        CancellationToken cancellationToken)
    {
        if (transaction.SupportsSavepoints)
        {
            await nativeOperation();
            return;
        }
        if (_store is not DbOrderedEventStore)
            throw new NotSupportedException("自定义有序存储需要支持保存点的数据库事务。");
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 读取业务流处理状态。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">待读取状态的业务流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含业务流当前处理状态的异步操作。</returns>
    public Task<OrderedEventStreamState> GetStateAsync(string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken = default) =>
        GetStateCoreAsync(consumer, stream, cancellationToken);

    /// <summary>
    /// 读取业务流状态并在开启事务前完成消费者校验。
    /// </summary>
    private Task<OrderedEventStreamState> GetStateCoreAsync(string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        return RunAsync(transaction => _store.GetStateAsync(transaction, consumer, stream, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 读取仍有待处理事件的业务流。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="limit">本次最多读取的业务流数量。</param>
    /// <param name="afterStreamKey">从该稳定流键之后继续读取；为空时从首个流开始。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含待处理业务流集合的异步操作。</returns>
    public Task<IReadOnlyList<OrderedEventStream>> GetPendingStreamsAsync(string consumer, int limit,
        string afterStreamKey = null, CancellationToken cancellationToken = default) =>
        GetPendingStreamsCoreAsync(consumer, limit, afterStreamKey, cancellationToken);

    /// <summary>
    /// 读取待处理业务流并在开启事务前完成消费者校验。
    /// </summary>
    private Task<IReadOnlyList<OrderedEventStream>> GetPendingStreamsCoreAsync(string consumer, int limit,
        string afterStreamKey, CancellationToken cancellationToken)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        return RunAsync(transaction => _store.GetPendingStreamsAsync(transaction, consumer, limit, afterStreamKey, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 读取逻辑消费者的全局待处理事件数。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存储支持统计契约时返回待处理数量，否则返回 <see langword="null"/>。</returns>
    public async Task<long?> GetPendingEventCountAsync(string consumer,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (_store is not IOrderedEventStoreStatistics statistics)
            return null;
        return await RunAsync(transaction => statistics.GetPendingEventCountAsync(transaction, consumer,
            cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 读取逻辑消费者的全局待处理业务流数量。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存储支持统计契约时返回待处理流数量，否则返回 <see langword="null"/>。</returns>
    public async Task<long?> GetPendingStreamCountAsync(string consumer,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (_store is not IOrderedEventStorePendingStreamStatistics statistics)
            return null;
        return await RunAsync(transaction => statistics.GetPendingStreamCountAsync(transaction, consumer,
            cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 读取逻辑消费者最老待处理事件的年龄。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>最老积压年龄；存储不支持统计或没有积压时返回 <see langword="null"/>。</returns>
    public async Task<TimeSpan?> GetOldestPendingEventAgeAsync(string consumer,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (_store is not IOrderedEventStorePendingAgeStatistics statistics)
            return null;
        var created = await RunAsync(transaction => statistics.GetOldestPendingEventUtcAsync(transaction, consumer,
            cancellationToken), cancellationToken);
        if (!created.HasValue)
            return null;
        return MaxAge(DateTime.UtcNow - created.Value);
    }

    /// <summary>
    /// 清理指定消费者已经完成且超过留存期限的收件记录。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cutoffUtc">留存截止时间，必须为 UTC。</param>
    /// <param name="batchSize">本次最多清理的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存储支持清理时返回删除数量，否则返回 <see langword="null"/>。</returns>
    /// <remarks>只删除已完成收件记录；待处理和失败记录必须保留以支持恢复与重放。</remarks>
    public async Task<int?> DeleteProcessedBeforeAsync(string consumer, DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("留存截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (_store is not IOrderedEventStoreRetention retention)
            return null;
        return await RunAsync(transaction => retention.DeleteProcessedBeforeAsync(transaction, consumer, cutoffUtc,
            batchSize, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 清理指定消费者超过重放窗口的已处理事件历史记录。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cutoffUtc">历史记录截止时间，必须为 UTC。</param>
    /// <param name="batchSize">本次最多清理的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存储支持历史清理时返回删除数量，否则返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 该入口不会被有序事件留存调度器自动调用。删除后超出保留窗口的事件 ID 可能再次被接收，
    /// 调用方必须先确认业务幂等记录和允许重放范围。
    /// </remarks>
    public async Task<int?> DeleteArchivedBeforeAsync(string consumer, DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("历史留存截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (_store is not IOrderedEventStoreArchiveRetention retention)
            return null;
        return await RunAsync(transaction => retention.DeleteArchivedBeforeAsync(transaction, consumer, cutoffUtc,
            batchSize, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 将时钟异常或未来时间归一化为非负年龄。
    /// </summary>
    /// <param name="age">原始年龄。</param>
    /// <returns>非负年龄。</returns>
    private static TimeSpan MaxAge(TimeSpan age) => age < TimeSpan.Zero ? TimeSpan.Zero : age;

    /// <summary>
    /// 允许失败的前序事件重试。
    /// </summary>
    /// <remarks>宿主必须对人工重试入口授权并记录审计，不能自动跳过失败序号；仅在恢复事务确认提交后增加重试指标。</remarks>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">待恢复的业务流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task RetryAsync(string consumer, OrderedEventStream stream, CancellationToken cancellationToken = default) =>
        RetryCoreAsync(consumer, stream, true, cancellationToken);

    /// <summary>
    /// 在调用方已完成授权后重试失败事件。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">待恢复的业务流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示恢复操作的异步任务。</returns>
    /// <remarks>仅供 <see cref="IOrderedEventRecoveryService"/> 使用，避免恢复服务重复执行授权器。</remarks>
    internal Task RetryWithoutAuthorizationAsync(string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken = default) => RetryCoreAsync(consumer, stream, false, cancellationToken);

    /// <summary>
    /// 执行人工恢复前的授权检查并记录已提交的重试。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">待恢复的业务流。</param>
    /// <param name="authorize">是否在事务开始前执行授权器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task RetryCoreAsync(string consumer, OrderedEventStream stream, bool authorize,
        CancellationToken cancellationToken)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (authorize)
        {
            if (_authorizers.Count == 0)
                throw new InvalidOperationException("有序事件恢复未配置授权器，已拒绝恢复操作。");
            foreach (var authorizer in _authorizers)
                await authorizer.AuthorizeAsync(consumer, stream, cancellationToken);
        }

        try
        {
            await RunAsync(async transaction =>
            {
                await _store.RetryAsync(transaction, consumer, stream, cancellationToken);
                return true;
            }, cancellationToken);
        }
        catch (Exception exception) when (exception.GetTransactionOutcome() == TransactionOutcome.Committed)
        {
            RecordRetry(consumer);
            throw;
        }

        RecordRetry(consumer);
    }

    /// <summary>
    /// 记录指定消费者已确认提交的恢复重试。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    private void RecordRetry(string consumer)
    {
        foreach (var metric in _metrics)
        {
            if (string.Equals(metric.ConsumerName, consumer, StringComparison.Ordinal))
            {
                metric.RecordRetry();
            }
        }
    }

    /// <summary>
    /// 执行事务并保留提交结果与清理错误。
    /// </summary>
    /// <typeparam name="T">事务操作的结果类型。</typeparam>
    /// <param name="action">在当前数据库事务内执行的操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>事务操作返回的结果。</returns>
    private async Task<T> RunAsync<T>(Func<DbTransaction, Task<T>> action, CancellationToken cancellationToken)
        => await RunTransactionAsync(owner => action(owner.Transaction), cancellationToken);

    /// <summary>
    /// 执行由适配器拥有的事务并保留提交结果与清理错误。
    /// </summary>
    /// <typeparam name="T">事务操作的结果类型。</typeparam>
    /// <param name="action">使用当前事务所有者执行的操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>事务操作返回的结果。</returns>
    private async Task<T> RunTransactionAsync<T>(Func<IOrderedEventTransaction, Task<T>> action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transaction = await _transactions.BeginAsync(cancellationToken);
        Exception failure = null;
        var commitStarted = false;
        var outcome = TransactionOutcome.Unknown;
        T result = default;
        try
        {
            var state = transaction as IOrderedEventTransactionState;
            state?.ActivateAmbient();
            result = await action(transaction);
            cancellationToken.ThrowIfCancellationRequested();
            state?.ActivateAmbient();
            commitStarted = true;
            await transaction.CommitAsync(cancellationToken);
            outcome = TransactionOutcome.Committed;
        }
        catch (Exception exception)
        {
            failure = exception is BusinessFailure businessFailure ? businessFailure.Original : exception;
            if (exception is BusinessFailure)
            {
                try
                {
                    // 业务回调的子上下文重置后，在本轮提交上下文重新激活空缓冲。
                    (transaction as IOrderedEventTransactionState)?.ActivateAmbient();
                    commitStarted = true;
                    await transaction.CommitAsync(CancellationToken.None);
                    outcome = TransactionOutcome.Committed;
                    failure.Data["Bing.OrderedEventFailureRecorded"] = true;
                }
                catch (Exception recordingException)
                {
                    failure.Data["Bing.OrderedEventFailureRecordingException"] = recordingException;
                    outcome = await RollbackAsync(transaction, failure, commitStarted);
                    if (outcome == TransactionOutcome.Committed)
                        failure.Data["Bing.OrderedEventFailureRecorded"] = true;
                }
            }
            else
            {
                outcome = await RollbackAsync(transaction, failure, commitStarted);
            }
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync();
            }
            catch (Exception disposalException)
            {
                if (failure == null)
                    failure = disposalException;
                else
                    failure.Data["Bing.TransactionCleanupException"] = disposalException;
            }
        }
        if (failure != null)
        {
            failure.SetTransactionOutcome(outcome);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return result;
    }

    /// <summary>
    /// 回滚事务并保留清理错误。
    /// </summary>
    /// <param name="transaction">待回滚的有序事件事务。</param>
    /// <param name="failure">用于记录回滚异常的原始异常。</param>
    /// <param name="commitStarted">是否已经开始提交操作。</param>
    /// <returns>能够确认的事务结果。</returns>
    private static async Task<TransactionOutcome> RollbackAsync(IOrderedEventTransaction transaction,
        Exception failure, bool commitStarted)
    {
        // 适配器已确认数据库提交后的缓冲发送错误，不能再解释为回滚或未知提交。
        if (transaction is IOrderedEventTransactionState { Outcome: TransactionOutcome.Committed })
            return TransactionOutcome.Committed;
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return commitStarted ? TransactionOutcome.Unknown : TransactionOutcome.RolledBack;
        }
        catch (Exception rollbackException)
        {
            failure.Data["Bing.TransactionRollbackException"] = rollbackException;
            return TransactionOutcome.Unknown;
        }
    }

    /// <summary>
    /// 传递已回滚业务且需要提交失败标记的内部信号。
    /// </summary>
    private sealed class BusinessFailure : Exception
    {
        /// <summary>
        /// 初始化一个 <see cref="BusinessFailure" /> 类型的实例。
        /// </summary>
        /// <param name="original">业务处理期间捕获的原始异常。</param>
        internal BusinessFailure(Exception original) : base("有序业务已回滚，等待提交失败状态。", original) =>
            Original = original;

        /// <summary>
        /// 获取原始业务异常。
        /// </summary>
        internal Exception Original { get; }
    }
}
