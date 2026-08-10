using Dapper;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper 多结果集读取结果。
/// </summary>
internal sealed class SqlMultipleQueryResult : ISqlMultipleQueryResult
{
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
    /// 初始化一个<see cref="SqlMultipleQueryResult"/>类型的实例。
    /// </summary>
    /// <param name="reader">Dapper 结果集读取器。</param>
    /// <param name="executionLease">当前执行租约。</param>
    /// <param name="complete">执行完成回调。</param>
    public SqlMultipleQueryResult(SqlMapper.GridReader reader, IDisposable executionLease, Action<bool, Exception> complete)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _executionLease = executionLease ?? throw new ArgumentNullException(nameof(executionLease));
        _complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    /// <summary>
    /// 初始化一个支持异步完成的<see cref="SqlMultipleQueryResult"/>类型的实例。
    /// </summary>
    /// <param name="reader">Dapper 结果集读取器。</param>
    /// <param name="executionLease">当前执行租约。</param>
    /// <param name="complete">异步执行完成回调。</param>
    public SqlMultipleQueryResult(SqlMapper.GridReader reader, IDisposable executionLease,
        Func<bool, Exception, Task> complete)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _executionLease = executionLease ?? throw new ArgumentNullException(nameof(executionLease));
        _completeAsync = complete ?? throw new ArgumentNullException(nameof(complete));
    }

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
        var reader = Interlocked.Exchange(ref _reader, null);
        if (reader == null)
            return;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var completed = false;
        try
        {
            completed = reader.IsConsumed;
            reader.Dispose();
        }
        catch (Exception currentException)
        {
            primaryException = currentException;
        }
        Complete(completed && primaryException == null, primaryException, cleanupExceptions);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var reader = Interlocked.Exchange(ref _reader, null);
        if (reader == null)
            return;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var completed = false;
        try
        {
            completed = reader.IsConsumed;
            await SqlTransactionAsyncAdapter.DisposeAsync(reader).ConfigureAwait(false);
        }
        catch (Exception currentException)
        {
            primaryException = currentException;
        }
        await CompleteAsync(completed && primaryException == null, primaryException, cleanupExceptions)
            .ConfigureAwait(false);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
    }

    /// <summary>
    /// 同步读取当前结果集，并在失败时完成资源清理。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="read">读取操作。</param>
    /// <returns>当前结果集。</returns>
    private TResult Read<TResult>(Func<SqlMapper.GridReader, TResult> read)
    {
        var reader = GetReader();
        try
        {
            return read(reader);
        }
        catch (Exception exception)
        {
            DisposeAfterFailure(exception);
            throw;
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
        var reader = GetReader();
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
    }

    /// <summary>
    /// 获取当前可用读取器。
    /// </summary>
    /// <returns>Dapper 结果集读取器。</returns>
    private SqlMapper.GridReader GetReader() => _reader ?? throw new ObjectDisposedException(nameof(SqlMultipleQueryResult));

    /// <summary>
    /// 处理读取失败。
    /// </summary>
    /// <param name="exception">读取异常。</param>
    private void DisposeAfterFailure(Exception exception)
    {
        var reader = Interlocked.Exchange(ref _reader, null);
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
        var reader = Interlocked.Exchange(ref _reader, null);
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
        try
        {
            if (complete != null)
                complete(completed, exception);
            else
                Interlocked.Exchange(ref _completeAsync, null)?.Invoke(completed, exception).GetAwaiter().GetResult();
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
    private async Task CompleteAsync(bool completed, Exception exception, ICollection<Exception> cleanupExceptions)
    {
        var completeAsync = Interlocked.Exchange(ref _completeAsync, null);
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
            var complete = Interlocked.Exchange(ref _complete, null);
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