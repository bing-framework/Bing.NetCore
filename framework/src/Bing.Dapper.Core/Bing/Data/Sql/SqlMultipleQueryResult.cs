using Dapper;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper 多结果集读取结果。
/// </summary>
internal sealed class SqlMultipleQueryResult : ISqlMultipleQueryResult
{
    /// <summary>
    /// 保护读取器与当前读取或释放操作的同步锁。
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    /// Dapper 结果集读取器。
    /// </summary>
    private SqlMapper.GridReader _reader;

    /// <summary>
    /// 执行完成回调。
    /// </summary>
    private Action<bool, Exception> _complete;

    /// <summary>
    /// 异步执行完成回调。
    /// </summary>
    private Func<bool, Exception, Task> _completeAsync;

    /// <summary>
    /// 执行租约。
    /// </summary>
    private IDisposable _executionLease;

    /// <summary>
    /// 指示当前读取器正在读取或释放，禁止并发访问同一结果集。
    /// </summary>
    private bool _operationInProgress;

    /// <summary>
    /// 初始化支持同步和异步完成的<see cref="SqlMultipleQueryResult"/>类型的实例。
    /// </summary>
    /// <param name="reader">Dapper 结果集读取器。</param>
    /// <param name="executionLease">当前执行租约。</param>
    /// <param name="complete">同步释放时使用的完成回调。</param>
    /// <param name="completeAsync">异步释放时使用的完成回调。</param>
    internal SqlMultipleQueryResult(SqlMapper.GridReader reader, IDisposable executionLease,
        Action<bool, Exception> complete, Func<bool, Exception, Task> completeAsync)
        : this(reader, executionLease, complete, completeAsync, currentReader => currentReader.IsConsumed,
            currentReader => currentReader.Dispose(), currentReader => SqlTransactionAsyncAdapter.DisposeAsync(currentReader))
    {
    }

    /// <summary>
    /// 使用指定生命周期回调初始化多结果集读取结果。
    /// </summary>
    /// <param name="reader">Dapper 结果集读取器。</param>
    /// <param name="executionLease">当前执行租约。</param>
    /// <param name="complete">同步释放时使用的完成回调。</param>
    /// <param name="completeAsync">异步释放时使用的完成回调。</param>
    /// <param name="isConsumed">判断读取器是否已完整消费的函数。</param>
    /// <param name="disposeReader">释放读取器的操作。</param>
    /// <param name="disposeReaderAsync">异步释放读取器的操作。</param>
    internal SqlMultipleQueryResult(SqlMapper.GridReader reader, IDisposable executionLease,
        Action<bool, Exception> complete, Func<bool, Exception, Task> completeAsync,
        Func<SqlMapper.GridReader, bool> isConsumed,
        Action<SqlMapper.GridReader> disposeReader,
        Func<SqlMapper.GridReader, Task> disposeReaderAsync)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _executionLease = executionLease ?? throw new ArgumentNullException(nameof(executionLease));
        _complete = complete ?? throw new ArgumentNullException(nameof(complete));
        _completeAsync = completeAsync ?? throw new ArgumentNullException(nameof(completeAsync));
        IsConsumed = isConsumed ?? throw new ArgumentNullException(nameof(isConsumed));
        DisposeReader = disposeReader ?? throw new ArgumentNullException(nameof(disposeReader));
        DisposeReaderAsync = disposeReaderAsync ?? throw new ArgumentNullException(nameof(disposeReaderAsync));
    }

    /// <summary>
    /// 判断读取器是否已完整消费。
    /// </summary>
    private Func<SqlMapper.GridReader, bool> IsConsumed { get; }

    /// <summary>
    /// 释放读取器的同步操作。
    /// </summary>
    private Action<SqlMapper.GridReader> DisposeReader { get; }

    /// <summary>
    /// 释放读取器的异步操作。
    /// </summary>
    private Func<SqlMapper.GridReader, Task> DisposeReaderAsync { get; }

    /// <inheritdoc />
    public List<dynamic> Read() => Read(reader => reader.Read().ToList());

    /// <inheritdoc />
    public List<TEntity> Read<TEntity>() => Read(reader => reader.Read<TEntity>().ToList());

    /// <inheritdoc />
    public async Task<List<dynamic>> ReadAsync(CancellationToken cancellationToken) => await ReadAsync(async reader =>
        (await reader.ReadAsync()).ToList(), cancellationToken);

    /// <inheritdoc />
    public async Task<List<TEntity>> ReadAsync<TEntity>(CancellationToken cancellationToken) => await ReadAsync(async reader =>
        (await reader.ReadAsync<TEntity>()).ToList(), cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        if (TryBeginDispose(out var reader) == false)
            return;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var completed = false;
        try
        {
            completed = IsConsumed(reader);
            DisposeReader(reader);
        }
        catch (Exception currentException)
        {
            primaryException = currentException;
        }
        try
        {
            Complete(completed && primaryException == null, primaryException, cleanupExceptions);
            SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>
    /// 异步释放多结果集读取器及其执行资源。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (TryBeginDispose(out var reader) == false)
            return;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var completed = false;
        try
        {
            completed = IsConsumed(reader);
            await DisposeReaderAsync(reader).ConfigureAwait(false);
        }
        catch (Exception currentException)
        {
            primaryException = currentException;
        }
        try
        {
            await CompleteAsync(completed && primaryException == null, primaryException, cleanupExceptions)
                .ConfigureAwait(false);
            SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>
    /// 同步读取当前结果集，并在失败时完成资源清理。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="read">读取操作。</param>
    /// <returns>当前结果集。</returns>
    private TResult Read<TResult>(Func<SqlMapper.GridReader, TResult> read)
    {
        var reader = BeginRead();
        try
        {
            return read(reader);
        }
        catch (Exception exception)
        {
            DisposeAfterFailure(exception);
            throw;
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>
    /// 异步读取当前结果集，并在失败时完成资源清理。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="read">异步读取操作。</param>
    /// <param name="cancellationToken">开始读取当前结果集前使用的取消令牌。</param>
    /// <returns>当前结果集。</returns>
    private async Task<TResult> ReadAsync<TResult>(Func<SqlMapper.GridReader, Task<TResult>> read,
        CancellationToken cancellationToken)
    {
        var reader = BeginRead();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await read(reader);
        }
        catch (Exception exception)
        {
            await DisposeAfterFailureAsync(exception).ConfigureAwait(false);
            throw;
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>
    /// 原子取得当前结果集的读取所有权。
    /// </summary>
    /// <returns>可供当前操作读取的 Dapper 读取器。</returns>
    private SqlMapper.GridReader BeginRead()
    {
        lock (_syncRoot)
        {
            if (_operationInProgress)
                throw new InvalidOperationException("当前多结果集正在读取或释放，不能并发访问。");
            if (_reader == null)
                throw new ObjectDisposedException(nameof(SqlMultipleQueryResult));
            _operationInProgress = true;
            return _reader;
        }
    }

    /// <summary>
    /// 原子取得读取器的释放所有权。
    /// </summary>
    /// <param name="reader">待释放的读取器。</param>
    /// <returns>是否需要由当前调用执行释放。</returns>
    private bool TryBeginDispose(out SqlMapper.GridReader reader)
    {
        lock (_syncRoot)
        {
            if (_operationInProgress)
                throw new InvalidOperationException("当前多结果集正在读取或释放，不能并发访问。");
            reader = _reader;
            if (reader == null)
                return false;
            _reader = null;
            _operationInProgress = true;
            return true;
        }
    }

    /// <summary>
    /// 结束当前读取或释放操作。
    /// </summary>
    private void EndOperation()
    {
        lock (_syncRoot)
            _operationInProgress = false;
    }

    /// <summary>
    /// 处理读取失败。
    /// </summary>
    /// <param name="exception">读取异常。</param>
    private void DisposeAfterFailure(Exception exception)
    {
        SqlMapper.GridReader reader;
        lock (_syncRoot)
        {
            reader = _reader;
            _reader = null;
        }
        var cleanupExceptions = new List<Exception>();
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => reader?.Dispose());
        Complete(false, exception, cleanupExceptions);
        SqlQueryPlanLifecycle.ThrowExceptions(exception, cleanupExceptions);
    }

    /// <summary>
    /// 异步处理读取失败。
    /// </summary>
    /// <param name="exception">读取异常。</param>
    private async Task DisposeAfterFailureAsync(Exception exception)
    {
        SqlMapper.GridReader reader;
        lock (_syncRoot)
        {
            reader = _reader;
            _reader = null;
        }
        var cleanupExceptions = new List<Exception>();
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions,
            () => SqlTransactionAsyncAdapter.DisposeAsync(reader)).ConfigureAwait(false);
        await CompleteAsync(false, exception, cleanupExceptions).ConfigureAwait(false);
        SqlQueryPlanLifecycle.ThrowExceptions(exception, cleanupExceptions);
    }

    /// <summary>
    /// 仅执行一次完成回调并归还租约。
    /// </summary>
    /// <param name="completed">是否完整读取全部结果集。</param>
    /// <param name="exception">读取异常。</param>
    /// <param name="cleanupExceptions">当前生命周期已捕获的清理异常。</param>
    private void Complete(bool completed, Exception exception, ICollection<Exception> cleanupExceptions)
    {
        var complete = Interlocked.Exchange(ref _complete, null);
        Interlocked.Exchange(ref _completeAsync, null);
        try
        {
            if (complete != null)
                complete(completed, exception);
        }
        catch (Exception completionException)
        {
            CaptureCompletionExceptions(cleanupExceptions, completionException, exception);
        }
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions,
            () => Interlocked.Exchange(ref _executionLease, null)?.Dispose());
    }

    /// <summary>
    /// 异步执行一次完成回调并归还租约。
    /// </summary>
    /// <param name="completed">是否完整读取全部结果集。</param>
    /// <param name="exception">读取异常。</param>
    /// <param name="cleanupExceptions">当前生命周期已捕获的清理异常。</param>
    private async Task CompleteAsync(bool completed, Exception exception, ICollection<Exception> cleanupExceptions)
    {
        var completeAsync = Interlocked.Exchange(ref _completeAsync, null);
        var complete = Interlocked.Exchange(ref _complete, null);
        if (completeAsync != null)
        {
            try
            {
                await completeAsync(completed, exception).ConfigureAwait(false);
            }
            catch (Exception completionException)
            {
                CaptureCompletionExceptions(cleanupExceptions, completionException, exception);
            }
        }
        else
        {
            try
            {
                complete?.Invoke(completed, exception);
            }
            catch (Exception completionException)
            {
                CaptureCompletionExceptions(cleanupExceptions, completionException, exception);
            }
        }
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions,
            () => SqlTransactionAsyncAdapter.DisposeAsync(Interlocked.Exchange(ref _executionLease, null)))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 捕获完成回调产生的清理异常，排除其重新抛出的同一主异常。
    /// </summary>
    /// <param name="cleanupExceptions">当前清理异常集合。</param>
    /// <param name="completionException">完成回调抛出的异常。</param>
    /// <param name="primaryException">读取或释放路径的原始异常。</param>
    private static void CaptureCompletionExceptions(ICollection<Exception> cleanupExceptions, Exception completionException,
        Exception primaryException)
    {
        IEnumerable<Exception> exceptions = completionException is AggregateException aggregateException
            ? aggregateException.Flatten().InnerExceptions
            : new[] { completionException };
        foreach (var exception in exceptions)
        {
            if (ReferenceEquals(exception, primaryException) == false)
                cleanupExceptions.Add(exception);
        }
    }
}