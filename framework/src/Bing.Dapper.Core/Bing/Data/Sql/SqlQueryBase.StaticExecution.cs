using System.Data.Common;

namespace Bing.Data.Sql;

public abstract partial class SqlQueryBase
{
    private enum QueryTerminal
    {
        First,
        FirstOrDefault,
        Single,
        SingleOrDefault
    }

    internal static IEnumerable<TResult> StreamStaticRows<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, SqlStaticResultReader<TResult> mapper, CommandType commandType)
    {
        DbCommand command = null;
        DbDataReader reader = null;
        Func<TResult> read = null;
        var opened = false;
        Exception primaryException = null;
        try
        {
            try
            {
                command = SqlStaticCommandRunner.CreateCommand(connection, sql, parameters, transaction, timeout, commandType);
                opened = connection.State != ConnectionState.Open;
                if (opened) connection.Open();
                reader = command.ExecuteReader();
                read = mapper.Bind(reader);
            }
            catch (Exception exception)
            {
                primaryException = exception;
                throw;
            }

            while (true)
            {
                var item = default(TResult);
                bool hasRow;
                try
                {
                    hasRow = reader.Read();
                    if (hasRow) item = read();
                }
                catch (Exception exception)
                {
                    primaryException = exception;
                    throw;
                }
                if (!hasRow) break;
                yield return item;
            }
        }
        finally
        {
            var cleanupExceptions = new SqlCleanupErrors();
            if (reader != null)
                SqlCleanupErrors.Dispose(cleanupExceptions, reader);
            if (opened)
                SqlCleanupErrors.Close(cleanupExceptions, connection);
            if (command != null)
                SqlCleanupErrors.Dispose(cleanupExceptions, command);
            SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        }
    }

    private SqlQueryPlan RequireDynamicQuery(SqlQueryPlan plan)
    {
        if (ExecutionMode == Configs.SqlExecutionMode.GeneratedOnly)
            throw new NotSupportedException("严格生成模式不支持 Dapper 多类型映射；请使用生成的单结果 DTO。");
        return plan;
    }

    private List<TResult> QueryStaticRows<TResult>(SqlQueryPlan plan, SqlStaticResultReader<TResult> mapper,
        int? timeout) => InternalQueryPlan(plan,
        (connection, sql, parameters, transaction) => CanExecuteStatically(parameters)
            ? ReadStaticRows(connection, sql, parameters, transaction, timeout, mapper, commandType: plan.CommandType)
            : SqlDynamicCommandRunner.Query<TResult>(connection, sql, parameters, transaction, true, timeout, plan.CommandType).AsBufferedList());

    private Task<List<TResult>> QueryStaticRowsAsync<TResult>(SqlQueryPlan plan, SqlStaticResultReader<TResult> mapper,
        int? timeout, CancellationToken token) => InternalQueryPlanAsync(plan,
        async (connection, sql, parameters, transaction, operationToken) => CanExecuteStatically(parameters)
            ? await ReadStaticRowsAsync(connection, sql, parameters, transaction, timeout, mapper, operationToken, commandType: plan.CommandType).ConfigureAwait(false)
            : (await SqlDynamicCommandRunner.QueryAsync<TResult>(connection, CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
                true, operationToken, plan.CommandType)).ConfigureAwait(false)).AsBufferedList(), token);

    private TResult QueryStaticTerminal<TResult>(SqlQueryPlan plan, SqlStaticResultReader<TResult> mapper,
        int? timeout, QueryTerminal terminal) => InternalQueryPlan(plan,
        (connection, sql, parameters, transaction) =>
        {
            if (!CanExecuteStatically(parameters))
                return ExecuteDynamicTerminal<TResult>(connection, sql, parameters, transaction, timeout, plan.CommandType, terminal);

            return SqlStaticCommandRunner.Execute(connection, sql, parameters, transaction, timeout, (command, errors) =>
                ExecuteWithReaderCleanup(command.ExecuteReader(), errors, reader =>
                {
                    var read = mapper.Bind(reader);
                    if (!reader.Read()) return CompleteTerminal(default(TResult), 0, terminal);
                    var result = read();
                    var count = 1;
                    if ((terminal is QueryTerminal.Single or QueryTerminal.SingleOrDefault) && reader.Read())
                    {
                        // 保留第二行转换错误先于基数错误的既有约定。
                        read(); count++;
                    }
                    return CompleteTerminal(result, count, terminal);
                }), plan.CommandType);
        });

    private Task<TResult> QueryStaticTerminalAsync<TResult>(SqlQueryPlan plan, SqlStaticResultReader<TResult> mapper,
        int? timeout, CancellationToken cancellationToken, QueryTerminal terminal) => InternalQueryPlanAsync(plan,
        async (connection, sql, parameters, transaction, operationToken) =>
        {
            if (!CanExecuteStatically(parameters))
                return await ExecuteDynamicTerminalAsync<TResult>(connection, sql, parameters, transaction, timeout,
                    plan.CommandType, operationToken, terminal).ConfigureAwait(false);

            return await SqlStaticCommandRunner.ExecuteAsync(connection, sql, parameters, transaction, timeout,
                async (command, readToken, errors) =>
                {
                    var reader = await command.ExecuteReaderAsync(readToken).ConfigureAwait(false);
                    return await ExecuteWithReaderCleanupAsync(reader, errors, async (current, token) =>
                    {
                        var read = mapper.Bind(current);
                        if (!await current.ReadAsync(token).ConfigureAwait(false)) return CompleteTerminal(default(TResult), 0, terminal);
                        var result = read();
                        var count = 1;
                        if ((terminal is QueryTerminal.Single or QueryTerminal.SingleOrDefault) && await current.ReadAsync(token).ConfigureAwait(false))
                        { read(); count++; }
                        return CompleteTerminal(result, count, terminal);
                    }, readToken).ConfigureAwait(false);
                }, operationToken, plan.CommandType).ConfigureAwait(false);
        }, cancellationToken);

    private static TResult CompleteTerminal<TResult>(TResult result, int count, QueryTerminal terminal)
    {
        if (count == 0 && (terminal is QueryTerminal.First or QueryTerminal.Single))
            return Enumerable.Empty<TResult>().First();
        if (count > 1) throw new InvalidOperationException("Sequence contains more than one element");
        return result;
    }

    private static TResult ExecuteDynamicTerminal<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, CommandType commandType, QueryTerminal terminal) => terminal switch
    {
        QueryTerminal.First => SqlDynamicCommandRunner.QueryFirst<TResult>(connection, sql, parameters, transaction,
            commandTimeout: timeout, commandType: commandType),
        QueryTerminal.FirstOrDefault => SqlDynamicCommandRunner.QueryFirstOrDefault<TResult>(connection, sql, parameters,
            transaction, commandTimeout: timeout, commandType: commandType),
        QueryTerminal.Single => SqlDynamicCommandRunner.QuerySingle<TResult>(connection, sql, parameters, transaction,
            commandTimeout: timeout, commandType: commandType),
        QueryTerminal.SingleOrDefault => SqlDynamicCommandRunner.QuerySingleOrDefault<TResult>(connection, sql, parameters,
            transaction, commandTimeout: timeout, commandType: commandType),
        _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null)
    };

    private Task<TResult> ExecuteDynamicTerminalAsync<TResult>(IDbConnection connection, string sql, object parameters,
        IDbTransaction transaction, int? timeout, CommandType commandType, CancellationToken cancellationToken,
        QueryTerminal terminal)
    {
        var command = CreateQueryCommandDefinition(sql, parameters, transaction, timeout, buffered: true,
            cancellationToken, commandType);
        return terminal switch
        {
            QueryTerminal.First => SqlDynamicCommandRunner.QueryFirstAsync<TResult>(connection, command),
            QueryTerminal.FirstOrDefault => SqlDynamicCommandRunner.QueryFirstOrDefaultAsync<TResult>(connection, command),
            QueryTerminal.Single => SqlDynamicCommandRunner.QuerySingleAsync<TResult>(connection, command),
            QueryTerminal.SingleOrDefault => SqlDynamicCommandRunner.QuerySingleOrDefaultAsync<TResult>(connection, command),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null)
        };
    }
}
