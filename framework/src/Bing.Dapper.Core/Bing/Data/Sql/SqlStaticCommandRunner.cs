using System.Data.Common;

namespace Bing.Data.Sql;

/// <summary>
/// 只消费准备好的命令和连接/事务的 ADO.NET 执行器，不解析 Builder、映射或环境工作单元。
/// </summary>
internal static class SqlStaticCommandRunner
{
    internal static DbCommand CreateCommand(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, CommandType commandType = CommandType.Text)
    {
        if (connection is not DbConnection db)
            throw new NotSupportedException("静态 SQL 执行要求 DbConnection。");
        if (parameters != null && parameters is not ISqlStaticParameterSource)
            throw new NotSupportedException("静态 SQL 执行要求生成参数或结构化 SqlParam 参数快照。");
        var command = db.CreateCommand();
        try
        {
            command.CommandText = sql;
            command.CommandType = commandType;
            command.Transaction = (DbTransaction)transaction;
            if (timeout.HasValue) command.CommandTimeout = timeout.Value;
            (parameters as ISqlStaticParameterSource)?.BindTo(command);
            return command;
        }
        catch (Exception exception)
        {
            var cleanupExceptions = new SqlCleanupErrors();
            SqlCleanupErrors.Dispose(cleanupExceptions, command);
            SqlQueryPlanLifecycle.ThrowExceptions(exception, cleanupExceptions);
            throw;
        }
    }

    internal static TResult Execute<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, Func<DbCommand, TResult> action,
        CommandType commandType = CommandType.Text) => Execute(connection, sql, parameters, transaction, timeout,
        (command, _) => action(command), commandType);

    internal static TResult Execute<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, Func<DbCommand, ICollection<Exception>, TResult> action,
        CommandType commandType = CommandType.Text)
    {
        var command = CreateCommand(connection, sql, parameters, transaction, timeout, commandType);
        var cleanupExceptions = new SqlCleanupErrors();
        var primaryException = default(Exception);
        var opened = false;
        var result = default(TResult);
        try
        {
            opened = connection.State != ConnectionState.Open;
            if (opened) connection.Open();
            result = action(command, cleanupExceptions);
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        finally
        {
            if (opened)
                SqlCleanupErrors.Close(cleanupExceptions, connection);
            SqlCleanupErrors.Dispose(cleanupExceptions, command);
        }
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

    internal static async Task<TResult> ExecuteAsync<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, Func<DbCommand, CancellationToken, Task<TResult>> action,
        CancellationToken token, CommandType commandType = CommandType.Text) => await ExecuteAsync(connection, sql,
        parameters, transaction, timeout, (command, operationToken, _) => action(command, operationToken), token,
        commandType).ConfigureAwait(false);

    internal static async Task<TResult> ExecuteAsync<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout,
        Func<DbCommand, CancellationToken, ICollection<Exception>, Task<TResult>> action, CancellationToken token,
        CommandType commandType = CommandType.Text)
    {
        var command = CreateCommand(connection, sql, parameters, transaction, timeout, commandType);
        var cleanupExceptions = new SqlCleanupErrors();
        var primaryException = default(Exception);
        var opened = false;
        var result = default(TResult);
        try
        {
            var db = (DbConnection)connection;
            opened = db.State != ConnectionState.Open;
            token.ThrowIfCancellationRequested();
            if (opened) await db.OpenAsync(token).ConfigureAwait(false);
            result = await action(command, token, cleanupExceptions).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        finally
        {
            if (opened)
                SqlCleanupErrors.Close(cleanupExceptions, connection);
            SqlCleanupErrors.Dispose(cleanupExceptions, command);
        }
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }
}
