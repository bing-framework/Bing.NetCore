using System.Data.Common;
using System.Runtime.CompilerServices;
using Bing.Data.Sql.Diagnostics;

namespace Bing.Data.Sql;

// Sql查询对象 - 流式查询
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 以非缓冲方式流式获取实体集合
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>实体流</returns>
    public IEnumerable<TEntity> StreamQuery<TEntity>(int? timeout = null)
    {
        EnsureStreamingSupported();
        return StreamQueryIterator<TEntity>(timeout);
    }

    /// <summary>
    /// 流式获取实体集合的迭代器
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>实体流</returns>
    private IEnumerable<TEntity> StreamQueryIterator<TEntity>(int? timeout)
    {
        DiagnosticsMessage message = null;
        var completed = false;
        var failed = false;
        IEnumerator<TEntity> enumerator = null;
        if (ExecuteBefore() == false)
        {
            ExecuteAfter((object)null);
            yield break;
        }
        try
        {
            var connection = GetExecutionConnection();
            var sql = GetSql();
            var dbParameters = GetDbParameters();
            var parameterMetadata = GetSqlParameterDiagnostics(SqlBuilder);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(sql, Params, connection, parameterMetadata);
            WriteTraceLog(sql, Params, GetDebugSql());
            enumerator = connection.Query<TEntity>(sql, dbParameters, transaction, false, timeout).GetEnumerator();
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            ExecuteAfter((object)null);
            throw;
        }

        try
        {
            using (enumerator)
            {
                while (true)
                {
                    TEntity item;
                    try
                    {
                        if (enumerator.MoveNext() == false)
                            break;
                        item = enumerator.Current;
                    }
                    catch (Exception e)
                    {
                        failed = true;
                        RollbackQueryTransaction();
                        ExecuteError(message, e);
                        throw;
                    }
                    yield return item;
                }
            }
            CompleteQueryTransaction();
            completed = true;
        }
        finally
        {
            if (completed == false)
                RollbackQueryTransaction();
            if (failed == false)
                ExecuteAfter(message);
            ExecuteAfter((object)null);
        }
    }

    /// <summary>
    /// 以非缓冲方式异步流式获取实体集合
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体异步流</returns>
    public async IAsyncEnumerable<TEntity> StreamQueryAsync<TEntity>(int? timeout = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureStreamingSupported();
        DiagnosticsMessage message = null;
        var completed = false;
        var failed = false;
        IDataReader reader = null;
        Func<IDataReader, TEntity> parser = null;
        if (ExecuteBefore() == false)
        {
            ExecuteAfter((object)null);
            yield break;
        }
        try
        {
            var connection = GetExecutionConnection();
            var sql = GetSql();
            var dbParameters = GetDbParameters();
            var parameterMetadata = GetSqlParameterDiagnostics(SqlBuilder);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(sql, Params, connection, parameterMetadata);
            WriteTraceLog(sql, Params, GetDebugSql());
            reader = await connection.ExecuteReaderAsync(new CommandDefinition(sql, dbParameters, transaction,
                timeout, cancellationToken: cancellationToken));
            parser = reader.GetRowParser<TEntity>();
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            ExecuteAfter((object)null);
            throw;
        }

        try
        {
            using (reader)
            {
                if (reader is DbDataReader dbReader)
                {
                    while (true)
                    {
                        TEntity item;
                        try
                        {
                            if (await ReadAsync(dbReader, cancellationToken) == false)
                                break;
                            item = parser(dbReader);
                        }
                        catch (Exception e)
                        {
                            failed = true;
                            RollbackQueryTransaction();
                            ExecuteError(message, e);
                            throw;
                        }
                        yield return item;
                    }
                }
                else
                {
                    while (true)
                    {
                        TEntity item;
                        try
                        {
                            if (Read(reader, cancellationToken) == false)
                                break;
                            item = parser(reader);
                        }
                        catch (Exception e)
                        {
                            failed = true;
                            RollbackQueryTransaction();
                            ExecuteError(message, e);
                            throw;
                        }
                        yield return item;
                    }
                }
            }
            CompleteQueryTransaction();
            completed = true;
        }
        finally
        {
            if (completed == false)
                RollbackQueryTransaction();
            if (failed == false)
                ExecuteAfter(message);
            ExecuteAfter((object)null);
        }
    }

    /// <summary>
    /// 以非缓冲方式异步逐行读取实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实体异步流。</returns>
    public IAsyncEnumerable<TEntity> StreamAsync<TEntity>(int? timeout = null,
        CancellationToken cancellationToken = default) => StreamQueryAsync<TEntity>(timeout, cancellationToken);

    /// <summary>
    /// 同步读取下一行并记录执行异常
    /// </summary>
    /// <param name="reader">数据读取器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>存在下一行时返回 true</returns>
    private bool Read(IDataReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return reader.Read();
    }

    /// <summary>
    /// 异步读取下一行并记录执行异常
    /// </summary>
    /// <param name="reader">数据读取器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>存在下一行时返回 true</returns>
    private async Task<bool> ReadAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        return await reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// 确保当前主库读取策略支持流式查询
    /// </summary>
    private void EnsureStreamingSupported()
    {
        var context = Options.GetDatabaseContext();
        if (context?.ReadPreference == SqlReadPreference.Primary &&
            context.DataSource?.PrimaryReadStrategy == PrimaryReadStrategy.Transaction)
            throw new InvalidOperationException("PrimaryReadStrategy.Transaction 不支持流式查询，请改用缓冲查询或 PrimaryDataSource 策略。");
    }
}