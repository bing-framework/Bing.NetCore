namespace Bing.Data.Sql;

/// <summary>
/// 提供 SQL 查询计划的终结执行入口。
/// </summary>
public abstract partial class SqlQueryBase
{
    /// <inheritdoc />
    List<TResult> ISqlQueryPlanExecutor.ToList<TResult>(SqlQueryPlan plan, int? timeout) =>
        TryCreateStaticReader<TResult>(out var reader) ? QueryStaticRows(plan, reader, timeout) :
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.Query<TResult>(connection, sql, parameters,
            transaction, buffered: true, commandTimeout: timeout, commandType: plan.CommandType).AsBufferedList());

    /// <inheritdoc />
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TResult> map, int? timeout) => InternalQueryPlan(RequireDynamicQuery(plan),
        (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.Query(connection, sql, map, parameters, transaction, buffered: true,
            splitOn: plan.SplitOn, commandTimeout: timeout, commandType: plan.CommandType).AsBufferedList());

    /// <inheritdoc />
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout) => InternalQueryPlan(RequireDynamicQuery(plan),
        (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.Query(connection, sql, map, parameters, transaction, buffered: true,
            splitOn: plan.SplitOn, commandTimeout: timeout, commandType: plan.CommandType).AsBufferedList());

    /// <inheritdoc />
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TFourth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout) => InternalQueryPlan(RequireDynamicQuery(plan),
        (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.Query(connection, sql, map, parameters, transaction, buffered: true,
            splitOn: plan.SplitOn, commandTimeout: timeout, commandType: plan.CommandType).AsBufferedList());

    /// <inheritdoc />
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout) => InternalQueryPlan(RequireDynamicQuery(plan),
        (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.Query(connection, sql, map, parameters, transaction, buffered: true,
            splitOn: plan.SplitOn, commandTimeout: timeout, commandType: plan.CommandType).AsBufferedList());

    /// <inheritdoc />
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout) =>
        InternalQueryPlan(RequireDynamicQuery(plan), (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.Query(connection, sql, map, parameters,
            transaction, buffered: true, splitOn: plan.SplitOn, commandTimeout: timeout,
            commandType: plan.CommandType).AsBufferedList());

    /// <inheritdoc />
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map,
        int? timeout) => InternalQueryPlan(RequireDynamicQuery(plan), (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.Query(connection, sql,
        map, parameters, transaction, buffered: true, splitOn: plan.SplitOn, commandTimeout: timeout,
        commandType: plan.CommandType).AsBufferedList());

    /// <inheritdoc />
    TResult ISqlQueryPlanExecutor.First<TResult>(SqlQueryPlan plan, int? timeout) =>
        TryCreateStaticReader<TResult>(out var reader) ? QueryStaticTerminal(plan, reader, timeout, QueryTerminal.First) :
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.QueryFirst<TResult>(connection, sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    TResult ISqlQueryPlanExecutor.FirstOrDefault<TResult>(SqlQueryPlan plan, int? timeout) =>
        TryCreateStaticReader<TResult>(out var reader) ? QueryStaticTerminal(plan, reader, timeout, QueryTerminal.FirstOrDefault) :
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.QueryFirstOrDefault<TResult>(connection, sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    TResult ISqlQueryPlanExecutor.Single<TResult>(SqlQueryPlan plan, int? timeout) =>
        TryCreateStaticReader<TResult>(out var reader) ? QueryStaticTerminal(plan, reader, timeout, QueryTerminal.Single) :
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.QuerySingle<TResult>(connection, sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    TResult ISqlQueryPlanExecutor.SingleOrDefault<TResult>(SqlQueryPlan plan, int? timeout) =>
        TryCreateStaticReader<TResult>(out var reader) ? QueryStaticTerminal(plan, reader, timeout, QueryTerminal.SingleOrDefault) :
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => SqlDynamicCommandRunner.QuerySingleOrDefault<TResult>(connection, sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    TResult ISqlQueryPlanExecutor.Scalar<TResult>(SqlQueryPlan plan, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => CanExecuteStatically(parameters)
            ? SqlStaticCommandRunner.Execute(connection, sql, parameters, transaction, timeout, command =>
                ConvertScalar<TResult>(command.ExecuteScalar()), plan.CommandType)
            : SqlDynamicCommandRunner.ExecuteScalar<TResult>(connection, sql, parameters, transaction,
                commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    IEnumerable<TResult> ISqlQueryPlanExecutor.AsEnumerable<TResult>(SqlQueryPlan plan, int? timeout) =>
        StreamQueryPlan<TResult>(plan, timeout);

    /// <inheritdoc />
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) =>
        TryCreateStaticReader<TResult>(out var reader) ? QueryStaticRowsAsync(plan, reader, timeout, cancellationToken) :
        InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction, operationToken) => (await SqlDynamicCommandRunner.QueryAsync<TResult>(connection, CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, operationToken, plan.CommandType))).AsBufferedList(), cancellationToken);

    /// <inheritdoc />
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TResult> map, int? timeout, CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(RequireDynamicQuery(plan), async (connection, sql, parameters, transaction, operationToken) =>
            (await SqlDynamicCommandRunner.QueryAsync(connection, CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
                buffered: true, operationToken, plan.CommandType), map, splitOn: plan.SplitOn)).AsBufferedList(), cancellationToken);

    /// <inheritdoc />
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout, CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(RequireDynamicQuery(plan), async (connection, sql, parameters, transaction, operationToken) =>
            (await SqlDynamicCommandRunner.QueryAsync(connection, CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
                buffered: true, operationToken, plan.CommandType), map, splitOn: plan.SplitOn)).AsBufferedList(), cancellationToken);

    /// <inheritdoc />
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout, CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(RequireDynamicQuery(plan), async (connection, sql, parameters, transaction, operationToken) =>
            (await SqlDynamicCommandRunner.QueryAsync(connection, CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
                buffered: true, operationToken, plan.CommandType), map, splitOn: plan.SplitOn)).AsBufferedList(), cancellationToken);

    /// <inheritdoc />
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(RequireDynamicQuery(plan), async (connection, sql, parameters,
        transaction, operationToken) => (await SqlDynamicCommandRunner.QueryAsync(connection, CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
            buffered: true, operationToken, plan.CommandType), map, splitOn: plan.SplitOn)).AsBufferedList(), cancellationToken);

    /// <inheritdoc />
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(RequireDynamicQuery(plan), async (connection, sql, parameters,
        transaction, operationToken) => (await SqlDynamicCommandRunner.QueryAsync(connection, CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
            buffered: true, operationToken, plan.CommandType), map, splitOn: plan.SplitOn)).AsBufferedList(), cancellationToken);

    /// <inheritdoc />
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh,
        TResult>(SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map,
        int? timeout, CancellationToken cancellationToken) => InternalQueryPlanAsync(RequireDynamicQuery(plan), async (connection, sql,
        parameters, transaction, operationToken) => (await SqlDynamicCommandRunner.QueryAsync(connection, CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, operationToken, plan.CommandType), map, splitOn: plan.SplitOn)).AsBufferedList(), cancellationToken);

    /// <inheritdoc />
    async Task<TResult> ISqlQueryPlanExecutor.FirstAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) =>
        TryCreateStaticReader<TResult>(out var reader)
            ? await QueryStaticTerminalAsync(plan, reader, timeout, cancellationToken, QueryTerminal.First).ConfigureAwait(false)
            : await InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction, operationToken) => await SqlDynamicCommandRunner.QueryFirstAsync<TResult>(connection, CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, operationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    async Task<TResult> ISqlQueryPlanExecutor.FirstOrDefaultAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) =>
        TryCreateStaticReader<TResult>(out var reader)
            ? await QueryStaticTerminalAsync(plan, reader, timeout, cancellationToken, QueryTerminal.FirstOrDefault).ConfigureAwait(false)
            : await InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction, operationToken) => await SqlDynamicCommandRunner.QueryFirstOrDefaultAsync<TResult>(connection, CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, operationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    async Task<TResult> ISqlQueryPlanExecutor.SingleAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) =>
        TryCreateStaticReader<TResult>(out var reader)
            ? await QueryStaticTerminalAsync(plan, reader, timeout, cancellationToken, QueryTerminal.Single).ConfigureAwait(false)
            : await InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction, operationToken) => await SqlDynamicCommandRunner.QuerySingleAsync<TResult>(connection, CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, operationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    async Task<TResult> ISqlQueryPlanExecutor.SingleOrDefaultAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) =>
        TryCreateStaticReader<TResult>(out var reader)
            ? await QueryStaticTerminalAsync(plan, reader, timeout, cancellationToken, QueryTerminal.SingleOrDefault).ConfigureAwait(false)
            : await InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction, operationToken) => await SqlDynamicCommandRunner.QuerySingleOrDefaultAsync<TResult>(connection, CreateQueryCommandDefinition(sql,
            parameters, transaction, timeout, buffered: true, operationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    Task<TResult> ISqlQueryPlanExecutor.ScalarAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan,
        (connection, sql, parameters, transaction, token) => CanExecuteStatically(parameters)
            ? SqlStaticCommandRunner.ExecuteAsync(connection, sql, parameters, transaction, timeout, async (command, cancellation) =>
                ConvertScalar<TResult>(await command.ExecuteScalarAsync(cancellation).ConfigureAwait(false)), token, plan.CommandType)
            : SqlDynamicCommandRunner.ExecuteScalarAsync<TResult>(connection, CreateQueryCommandDefinition(sql, parameters,
                transaction, timeout, buffered: true, token, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    IAsyncEnumerable<TResult> ISqlQueryPlanExecutor.AsAsyncEnumerable<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => StreamQueryPlanAsync<TResult>(plan, timeout, cancellationToken);
}
