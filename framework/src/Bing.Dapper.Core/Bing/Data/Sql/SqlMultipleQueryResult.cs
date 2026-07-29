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

    /// <inheritdoc />
    public List<dynamic> Read() => Read(reader => reader.Read().ToList());

    /// <inheritdoc />
    public List<TEntity> Read<TEntity>() => Read(reader => reader.Read<TEntity>().ToList());

    /// <inheritdoc />
    [Obsolete("请使用接收 CancellationToken 的 ReadAsync 重载")]
    public Task<List<dynamic>> ReadAsync() => ReadAsync(CancellationToken.None);

    /// <inheritdoc />
    [Obsolete("请使用接收 CancellationToken 的 ReadAsync 重载")]
    public Task<List<TEntity>> ReadAsync<TEntity>() => ReadAsync<TEntity>(CancellationToken.None);

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
        Exception exception = null;
        var completed = false;
        try
        {
            completed = reader.IsConsumed;
            reader.Dispose();
        }
        catch (Exception currentException)
        {
            exception = currentException;
            throw;
        }
        finally
        {
            Complete(completed && exception == null, exception);
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
            DisposeAfterFailure(exception);
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
        try
        {
            reader?.Dispose();
        }
        finally
        {
            Complete(false, exception);
        }
    }

    /// <summary>
    /// 仅执行一次完成回调并归还租约。
    /// </summary>
    /// <param name="completed">是否完整读取全部结果集。</param>
    /// <param name="exception">读取异常。</param>
    private void Complete(bool completed, Exception exception)
    {
        var complete = Interlocked.Exchange(ref _complete, null);
        try
        {
            complete?.Invoke(completed, exception);
        }
        finally
        {
            Interlocked.Exchange(ref _executionLease, null)?.Dispose();
        }
    }
}