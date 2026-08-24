using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 结果类型由终结方法选择的存储过程查询描述。
/// </summary>
public sealed class SqlProcedureQuery
{
    private readonly ISqlQueryPlanExecutor _executor;
    private readonly object _parameters;

    internal SqlProcedureQuery(ISqlQueryPlanExecutor executor, string procedure, object parameters)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (string.IsNullOrWhiteSpace(procedure))
            throw new ArgumentException("存储过程名称不能为空。", nameof(procedure));
        Procedure = procedure;
        _parameters = SqlQueryPlan.SnapshotParameters(parameters);
    }

    /// <summary>获取要执行的存储过程名称。</summary>
    public string Procedure { get; }

    /// <summary>获取由参数绑定器处理的输入和输出参数源。</summary>
    public object Parameters => SqlQueryPlan.SnapshotParameters(_parameters);

    /// <summary>同步执行过程并完整物化结果列表。</summary>
    public SqlProcedureResult<List<TResult>> ExecuteList<TResult>(int? timeout = null) =>
        Execute(plan => _executor.ToList<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取第一行。</summary>
    public SqlProcedureResult<TResult> ExecuteFirst<TResult>(int? timeout = null) =>
        Execute(plan => _executor.First<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取第一行或默认值。</summary>
    public SqlProcedureResult<TResult> ExecuteFirstOrDefault<TResult>(int? timeout = null) =>
        Execute(plan => _executor.FirstOrDefault<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取唯一一行。</summary>
    public SqlProcedureResult<TResult> ExecuteSingle<TResult>(int? timeout = null) =>
        Execute(plan => _executor.Single<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取唯一一行或默认值。</summary>
    public SqlProcedureResult<TResult> ExecuteSingleOrDefault<TResult>(int? timeout = null) =>
        Execute(plan => _executor.SingleOrDefault<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取首行首列值。</summary>
    public SqlProcedureResult<TResult> ExecuteScalar<TResult>(int? timeout = null) =>
        Execute(plan => _executor.Scalar<TResult>(plan, timeout));

    /// <summary>异步执行过程并完整物化结果列表。</summary>
    public Task<SqlProcedureResult<List<TResult>>> ExecuteListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.ToListAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取第一行。</summary>
    public Task<SqlProcedureResult<TResult>> ExecuteFirstAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.FirstAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取第一行或默认值。</summary>
    public Task<SqlProcedureResult<TResult>> ExecuteFirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.FirstOrDefaultAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取唯一一行。</summary>
    public Task<SqlProcedureResult<TResult>> ExecuteSingleAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.SingleAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取唯一一行或默认值。</summary>
    public Task<SqlProcedureResult<TResult>> ExecuteSingleOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.SingleOrDefaultAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取首行首列值。</summary>
    public Task<SqlProcedureResult<TResult>> ExecuteScalarAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.ScalarAsync<TResult>(plan, timeout, cancellationToken));

    private SqlProcedureResult<TExecutionResult> Execute<TExecutionResult>(
        Func<SqlQueryPlan, TExecutionResult> operation)
    {
        var receiver = new OutputParametersReceiver();
        var result = operation(CreateExecutionPlan(receiver));
        return new SqlProcedureResult<TExecutionResult>(result, receiver.OutputParameters);
    }

    private async Task<SqlProcedureResult<TExecutionResult>> ExecuteAsync<TExecutionResult>(
        Func<SqlQueryPlan, Task<TExecutionResult>> operation)
    {
        var receiver = new OutputParametersReceiver();
        var result = await operation(CreateExecutionPlan(receiver)).ConfigureAwait(false);
        return new SqlProcedureResult<TExecutionResult>(result, receiver.OutputParameters);
    }

    private SqlQueryPlan CreateExecutionPlan(OutputParametersReceiver receiver)
    {
        var plan = GetPlan();
        plan.SetOutputParametersReceiver(receiver.Set, receiver.CreateSnapshot);
        return plan;
    }

    private sealed class OutputParametersReceiver
    {
        public ISqlOutputParameterAccessor OutputParameters { get; private set; }

        public void Set(ISqlOutputParameterAccessor outputParameters) => OutputParameters = outputParameters;

        public void CreateSnapshot() => OutputParameters = SqlOutputParameterSnapshot.Create(OutputParameters);
    }

    private SqlQueryPlan GetPlan() => SqlQueryPlan.Create(Procedure, _parameters, "Id", CommandType.StoredProcedure);
}