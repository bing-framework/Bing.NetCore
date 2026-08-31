namespace Bing.Data.Sql;

// Sql查询对象 - 独立查询计划终结入口
public abstract partial class SqlQueryBase
{
    /// <inheritdoc />
    /// <returns>完整物化的结果列表。</returns>
    List<TResult> ISqlQueryPlanExecutor.ToList<TResult>(SqlQueryPlan plan, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => connection.Query<TResult>(sql, parameters,
            transaction, buffered: true, commandTimeout: timeout, commandType: plan.CommandType).ToList());

    /// <inheritdoc />
    /// <returns>完整物化并映射后的结果列表。</returns>
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TResult> map, int? timeout) => InternalQueryPlan(plan,
        (connection, sql, parameters, transaction) => connection.Query(sql, map, parameters, transaction, buffered: true,
            splitOn: plan.SplitOn, commandTimeout: timeout, commandType: plan.CommandType).ToList());

    /// <inheritdoc />
    /// <returns>完整物化并映射后的结果列表。</returns>
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout) => InternalQueryPlan(plan,
        (connection, sql, parameters, transaction) => connection.Query(sql, map, parameters, transaction, buffered: true,
            splitOn: plan.SplitOn, commandTimeout: timeout, commandType: plan.CommandType).ToList());

    /// <inheritdoc />
    /// <returns>完整物化并映射后的结果列表。</returns>
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TFourth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout) => InternalQueryPlan(plan,
        (connection, sql, parameters, transaction) => connection.Query(sql, map, parameters, transaction, buffered: true,
            splitOn: plan.SplitOn, commandTimeout: timeout, commandType: plan.CommandType).ToList());

    /// <inheritdoc />
    /// <returns>完整物化并映射后的结果列表。</returns>
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout) => InternalQueryPlan(plan,
        (connection, sql, parameters, transaction) => connection.Query(sql, map, parameters, transaction, buffered: true,
            splitOn: plan.SplitOn, commandTimeout: timeout, commandType: plan.CommandType).ToList());

    /// <inheritdoc />
    /// <returns>完整物化并映射后的结果列表。</returns>
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => connection.Query(sql, map, parameters,
            transaction, buffered: true, splitOn: plan.SplitOn, commandTimeout: timeout,
            commandType: plan.CommandType).ToList());

    /// <inheritdoc />
    /// <returns>完整物化并映射后的结果列表。</returns>
    List<TResult> ISqlQueryPlanExecutor.ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map,
        int? timeout) => InternalQueryPlan(plan, (connection, sql, parameters, transaction) => connection.Query(sql,
        map, parameters, transaction, buffered: true, splitOn: plan.SplitOn, commandTimeout: timeout,
        commandType: plan.CommandType).ToList());

    /// <inheritdoc />
    /// <returns>第一行结果。</returns>
    TResult ISqlQueryPlanExecutor.First<TResult>(SqlQueryPlan plan, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => connection.QueryFirst<TResult>(sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    /// <returns>第一行结果或默认值。</returns>
    TResult ISqlQueryPlanExecutor.FirstOrDefault<TResult>(SqlQueryPlan plan, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => connection.QueryFirstOrDefault<TResult>(sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    /// <returns>唯一结果行。</returns>
    TResult ISqlQueryPlanExecutor.Single<TResult>(SqlQueryPlan plan, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => connection.QuerySingle<TResult>(sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    /// <returns>唯一结果行或默认值。</returns>
    TResult ISqlQueryPlanExecutor.SingleOrDefault<TResult>(SqlQueryPlan plan, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => connection.QuerySingleOrDefault<TResult>(sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    /// <returns>首行首列的标量值。</returns>
    TResult ISqlQueryPlanExecutor.Scalar<TResult>(SqlQueryPlan plan, int? timeout) =>
        InternalQueryPlan(plan, (connection, sql, parameters, transaction) => connection.ExecuteScalar<TResult>(sql,
            parameters, transaction, commandTimeout: timeout, commandType: plan.CommandType));

    /// <inheritdoc />
    /// <returns>结果行同步流。</returns>
    IEnumerable<TResult> ISqlQueryPlanExecutor.AsEnumerable<TResult>(SqlQueryPlan plan, int? timeout) =>
        StreamQueryPlan<TResult>(plan, timeout);

    /// <inheritdoc />
    /// <returns>表示最终结果列表的异步操作。</returns>
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction) => (await connection.QueryAsync<TResult>(CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, cancellationToken, plan.CommandType))).ToList(), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TResult> map, int? timeout, CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(plan, async (connection, sql, parameters, transaction) =>
            (await connection.QueryAsync(CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
                buffered: true, cancellationToken, plan.CommandType), map, splitOn: plan.SplitOn)).ToList(), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout, CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(plan, async (connection, sql, parameters, transaction) =>
            (await connection.QueryAsync(CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
                buffered: true, cancellationToken, plan.CommandType), map, splitOn: plan.SplitOn)).ToList(), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout, CancellationToken cancellationToken) =>
        InternalQueryPlanAsync(plan, async (connection, sql, parameters, transaction) =>
            (await connection.QueryAsync(CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
                buffered: true, cancellationToken, plan.CommandType), map, splitOn: plan.SplitOn)).ToList(), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction) => (await connection.QueryAsync(CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
            buffered: true, cancellationToken, plan.CommandType), map, splitOn: plan.SplitOn)).ToList(), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction) => (await connection.QueryAsync(CreateQueryCommandDefinition(sql, parameters, transaction, timeout,
            buffered: true, cancellationToken, plan.CommandType), map, splitOn: plan.SplitOn)).ToList(), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ISqlQueryPlanExecutor.ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh,
        TResult>(SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map,
        int? timeout, CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql,
        parameters, transaction) => (await connection.QueryAsync(CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, cancellationToken, plan.CommandType), map, splitOn: plan.SplitOn)).ToList(), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示第一行结果的异步操作。</returns>
    Task<TResult> ISqlQueryPlanExecutor.FirstAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction) => await connection.QueryFirstAsync<TResult>(CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, cancellationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示第一行结果或默认值的异步操作。</returns>
    Task<TResult> ISqlQueryPlanExecutor.FirstOrDefaultAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction) => await connection.QueryFirstOrDefaultAsync<TResult>(CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, cancellationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示唯一结果行的异步操作。</returns>
    Task<TResult> ISqlQueryPlanExecutor.SingleAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction) => await connection.QuerySingleAsync<TResult>(CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, cancellationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示唯一结果行或默认值的异步操作。</returns>
    Task<TResult> ISqlQueryPlanExecutor.SingleOrDefaultAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction) => await connection.QuerySingleOrDefaultAsync<TResult>(CreateQueryCommandDefinition(sql,
            parameters, transaction, timeout, buffered: true, cancellationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    /// <returns>表示标量结果的异步操作。</returns>
    Task<TResult> ISqlQueryPlanExecutor.ScalarAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => InternalQueryPlanAsync(plan, async (connection, sql, parameters,
        transaction) => await connection.ExecuteScalarAsync<TResult>(CreateQueryCommandDefinition(sql, parameters,
            transaction, timeout, buffered: true, cancellationToken, plan.CommandType)), cancellationToken);

    /// <inheritdoc />
    /// <returns>结果行异步流。</returns>
    IAsyncEnumerable<TResult> ISqlQueryPlanExecutor.AsAsyncEnumerable<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken) => StreamQueryPlanAsync<TResult>(plan, timeout, cancellationToken);
}