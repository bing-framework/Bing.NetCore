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
        IEnumerator<TEntity> enumerator = null;
        try
        {
            if (ExecuteBefore() == false)
                yield break;
            var connection = GetConnection();
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
                        RollbackQueryTransaction();
                        ExecuteError(message, e);
                        throw;
                    }
                    yield return item;
                }
            }
            CompleteQueryTransaction();
            ExecuteAfter(message);
            completed = true;
        }
        finally
        {
            if (completed == false)
                RollbackQueryTransaction();
            ExecuteAfter(null);
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
        IDataReader reader = null;
        Func<IDataReader, TEntity> parser = null;
        try
        {
            if (ExecuteBefore() == false)
                yield break;
            var connection = GetConnection();
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
            throw;
        }

        try
        {
            using (reader)
            {
                if (reader is DbDataReader dbReader)
                {
                    while (await ReadAsync(dbReader, cancellationToken, message))
                        yield return parser(dbReader);
                }
                else
                {
                    while (Read(reader, cancellationToken, message))
                        yield return parser(reader);
                }
            }
            CompleteQueryTransaction();
            ExecuteAfter(message);
            completed = true;
        }
        finally
        {
            if (completed == false)
                RollbackQueryTransaction();
            ExecuteAfter(null);
        }
    }

    /// <summary>
    /// 同步读取下一行并记录执行异常
    /// </summary>
    /// <param name="reader">数据读取器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="message">诊断消息</param>
    /// <returns>存在下一行时返回 true</returns>
    private bool Read(IDataReader reader, CancellationToken cancellationToken, DiagnosticsMessage message)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return reader.Read();
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
    }

    /// <summary>
    /// 异步读取下一行并记录执行异常
    /// </summary>
    /// <param name="reader">数据读取器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="message">诊断消息</param>
    /// <returns>存在下一行时返回 true</returns>
    private async Task<bool> ReadAsync(DbDataReader reader, CancellationToken cancellationToken, DiagnosticsMessage message)
    {
        try
        {
            return await reader.ReadAsync(cancellationToken);
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
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