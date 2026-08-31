using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 结果类型由终结方法选择的存储过程查询描述。
/// </summary>
public sealed class SqlProcedureQuery
{
    /// <summary>
    /// 存储过程查询计划执行器。
    /// </summary>
    private readonly ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 存储过程输入和输出参数的初始快照。
    /// </summary>
    private readonly object _parameters;

    /// <summary>
    /// 初始化一个 <see cref="SqlProcedureQuery"/> 类型的实例。
    /// </summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="procedure">要执行的存储过程名称。</param>
    /// <param name="parameters">存储过程参数。</param>
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
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含结果列表和输出参数的执行结果。</returns>
    public SqlProcedureResult<List<TResult>> ExecuteList<TResult>(int? timeout = null) =>
        Execute(plan => _executor.ToList<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取第一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含第一行结果和输出参数的执行结果。</returns>
    public SqlProcedureResult<TResult> ExecuteFirst<TResult>(int? timeout = null) =>
        Execute(plan => _executor.First<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取第一行或默认值。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含第一行或默认值以及输出参数的执行结果。</returns>
    public SqlProcedureResult<TResult> ExecuteFirstOrDefault<TResult>(int? timeout = null) =>
        Execute(plan => _executor.FirstOrDefault<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取唯一一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含唯一结果和输出参数的执行结果。</returns>
    public SqlProcedureResult<TResult> ExecuteSingle<TResult>(int? timeout = null) =>
        Execute(plan => _executor.Single<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取唯一一行或默认值。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含唯一结果或默认值以及输出参数的执行结果。</returns>
    public SqlProcedureResult<TResult> ExecuteSingleOrDefault<TResult>(int? timeout = null) =>
        Execute(plan => _executor.SingleOrDefault<TResult>(plan, timeout));

    /// <summary>同步执行过程并获取首行首列值。</summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含标量值和输出参数的执行结果。</returns>
    public SqlProcedureResult<TResult> ExecuteScalar<TResult>(int? timeout = null) =>
        Execute(plan => _executor.Scalar<TResult>(plan, timeout));

    /// <summary>异步执行过程并完整物化结果列表。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含结果列表和输出参数的异步执行结果。</returns>
    public Task<SqlProcedureResult<List<TResult>>> ExecuteListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.ToListAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取第一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含第一行结果和输出参数的异步执行结果。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteFirstAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.FirstAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取第一行或默认值。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含第一行或默认值以及输出参数的异步执行结果。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteFirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.FirstOrDefaultAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取唯一一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含唯一结果和输出参数的异步执行结果。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteSingleAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.SingleAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取唯一一行或默认值。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含唯一结果或默认值以及输出参数的异步执行结果。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteSingleOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.SingleOrDefaultAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>异步执行过程并获取首行首列值。</summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含标量值和输出参数的异步执行结果。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteScalarAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        _executor.ScalarAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>
    /// 创建执行计划并同步执行存储过程操作。
    /// </summary>
    /// <typeparam name="TExecutionResult">执行操作的结果类型。</typeparam>
    /// <param name="operation">接收执行计划并返回结果的操作。</param>
    /// <returns>包含操作结果和输出参数的执行结果。</returns>
    private SqlProcedureResult<TExecutionResult> Execute<TExecutionResult>(
        Func<SqlQueryPlan, TExecutionResult> operation)
    {
        var receiver = new OutputParametersReceiver();
        var result = operation(CreateExecutionPlan(receiver));
        return new SqlProcedureResult<TExecutionResult>(result, receiver.OutputParameters);
    }

    /// <summary>
    /// 创建执行计划并异步执行存储过程操作。
    /// </summary>
    /// <typeparam name="TExecutionResult">执行操作的结果类型。</typeparam>
    /// <param name="operation">接收执行计划并返回异步结果的操作。</param>
    /// <returns>包含操作结果和输出参数的异步执行结果。</returns>
    private async Task<SqlProcedureResult<TExecutionResult>> ExecuteAsync<TExecutionResult>(
        Func<SqlQueryPlan, Task<TExecutionResult>> operation)
    {
        var receiver = new OutputParametersReceiver();
        var result = await operation(CreateExecutionPlan(receiver)).ConfigureAwait(false);
        return new SqlProcedureResult<TExecutionResult>(result, receiver.OutputParameters);
    }

    /// <summary>
    /// 创建存储过程执行计划并注册输出参数接收器。
    /// </summary>
    /// <param name="receiver">输出参数接收器。</param>
    /// <returns>配置完成的存储过程查询计划。</returns>
    private SqlQueryPlan CreateExecutionPlan(OutputParametersReceiver receiver)
    {
        var plan = GetPlan();
        plan.SetOutputParametersReceiver(receiver.Set, receiver.CreateSnapshot);
        return plan;
    }

    /// <summary>
    /// 接收并冻结存储过程输出参数的内部对象。
    /// </summary>
    private sealed class OutputParametersReceiver
    {
        /// <summary>
        /// 获取输出参数访问器快照。
        /// </summary>
        public ISqlOutputParameterAccessor OutputParameters { get; private set; }

        /// <summary>
        /// 保存执行完成后的输出参数访问器。
        /// </summary>
        /// <param name="outputParameters">输出参数访问器。</param>
        public void Set(ISqlOutputParameterAccessor outputParameters) => OutputParameters = outputParameters;

        /// <summary>
        /// 将当前输出参数访问器替换为不可变快照。
        /// </summary>
        public void CreateSnapshot() => OutputParameters = SqlOutputParameterSnapshot.Create(OutputParameters);
    }

    /// <summary>
    /// 创建当前存储过程的查询计划。
    /// </summary>
    /// <returns>使用存储过程命令类型配置的查询计划。</returns>
    private SqlQueryPlan GetPlan() => SqlQueryPlan.Create(Procedure, _parameters, "Id", CommandType.StoredProcedure);
}