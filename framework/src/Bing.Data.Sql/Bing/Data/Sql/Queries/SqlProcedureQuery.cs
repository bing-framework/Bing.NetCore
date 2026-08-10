using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 指定结果类型的存储过程查询描述。
/// </summary>
/// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
/// <remarks>
/// 该描述仅公开过程专用终结方法，并固定以 <see cref="CommandType.StoredProcedure"/> 执行。
/// 传入的非字典参数对象保持原引用，以便 Dapper 输出参数在执行后回写到调用方对象。
/// </remarks>
public sealed class SqlProcedureQuery<TResult>
{
    /// <summary>
    /// 执行当前过程计划的根查询内部执行器。
    /// </summary>
    private readonly ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 使用根查询、存储过程名称和参数源初始化查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="procedure">要执行的存储过程名称。</param>
    /// <param name="parameters">由参数绑定器处理的输入和输出参数源。</param>
    internal SqlProcedureQuery(ISqlQueryPlanExecutor executor, string procedure, object parameters)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (string.IsNullOrWhiteSpace(procedure))
            throw new ArgumentException("存储过程名称不能为空。", nameof(procedure));
        Procedure = procedure;
        Parameters = SqlQueryPlan.SnapshotParameters(parameters);
    }

    /// <summary>
    /// 获取要执行的存储过程名称。
    /// </summary>
    public string Procedure { get; }

    /// <summary>
    /// 获取由参数绑定器处理的输入和输出参数源。
    /// </summary>
    public object Parameters { get; }

    /// <summary>
    /// 获取执行当前描述的内部执行器。
    /// </summary>
    private ISqlQueryPlanExecutor Executor => _executor;

    /// <summary>
    /// 同步执行过程并完整物化结果列表。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含结果列表及本次执行输出参数的过程结果。</returns>
    public SqlProcedureResult<List<TResult>> ExecuteList(int? timeout = null) =>
        Execute(plan => Executor.ToList<TResult>(plan, timeout));

    /// <summary>
    /// 同步执行过程并获取第一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含第一行及本次执行输出参数的过程结果。</returns>
    public SqlProcedureResult<TResult> ExecuteFirst(int? timeout = null) =>
        Execute(plan => Executor.First<TResult>(plan, timeout));

    /// <summary>
    /// 同步执行过程并获取第一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含第一行或默认值及本次执行输出参数的过程结果。</returns>
    public SqlProcedureResult<TResult> ExecuteFirstOrDefault(int? timeout = null) =>
        Execute(plan => Executor.FirstOrDefault<TResult>(plan, timeout));

    /// <summary>
    /// 同步执行过程并获取唯一一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含唯一行及本次执行输出参数的过程结果。</returns>
    public SqlProcedureResult<TResult> ExecuteSingle(int? timeout = null) =>
        Execute(plan => Executor.Single<TResult>(plan, timeout));

    /// <summary>
    /// 同步执行过程并获取唯一一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含唯一行或默认值及本次执行输出参数的过程结果。</returns>
    public SqlProcedureResult<TResult> ExecuteSingleOrDefault(int? timeout = null) =>
        Execute(plan => Executor.SingleOrDefault<TResult>(plan, timeout));

    /// <summary>
    /// 同步执行过程并获取首行首列值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含标量值及本次执行输出参数的过程结果。</returns>
    public SqlProcedureResult<TResult> ExecuteScalar(int? timeout = null) =>
        Execute(plan => Executor.Scalar<TResult>(plan, timeout));

    /// <summary>
    /// 异步执行过程并完整物化结果列表。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含结果列表及本次执行输出参数的过程结果的异步操作。</returns>
    public Task<SqlProcedureResult<List<TResult>>> ExecuteListAsync(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        Executor.ToListAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>
    /// 异步执行过程并获取第一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含第一行及本次执行输出参数的过程结果的异步操作。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteFirstAsync(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        Executor.FirstAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>
    /// 异步执行过程并获取第一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含第一行或默认值及本次执行输出参数的过程结果的异步操作。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteFirstOrDefaultAsync(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        Executor.FirstOrDefaultAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>
    /// 异步执行过程并获取唯一一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含唯一行及本次执行输出参数的过程结果的异步操作。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteSingleAsync(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        Executor.SingleAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>
    /// 异步执行过程并获取唯一一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含唯一行或默认值及本次执行输出参数的过程结果的异步操作。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteSingleOrDefaultAsync(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        Executor.SingleOrDefaultAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>
    /// 异步执行过程并获取首行首列值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含标量值及本次执行输出参数的过程结果的异步操作。</returns>
    public Task<SqlProcedureResult<TResult>> ExecuteScalarAsync(int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(plan =>
        Executor.ScalarAsync<TResult>(plan, timeout, cancellationToken));

    /// <summary>
    /// 执行过程并把本次绑定的输出参数封装到返回结果中。
    /// </summary>
    /// <typeparam name="TExecutionResult">终结入口实际返回的结果类型。</typeparam>
    /// <param name="operation">执行当前过程计划的终结操作。</param>
    /// <returns>包含终结结果及输出参数访问器的过程结果。</returns>
    private SqlProcedureResult<TExecutionResult> Execute<TExecutionResult>(
        Func<SqlQueryPlan, TExecutionResult> operation)
    {
        var receiver = new OutputParametersReceiver();
        var result = operation(CreateExecutionPlan(receiver));
        return new SqlProcedureResult<TExecutionResult>(result, receiver.OutputParameters);
    }

    /// <summary>
    /// 异步执行过程并把本次绑定的输出参数封装到返回结果中。
    /// </summary>
    /// <typeparam name="TExecutionResult">终结入口实际返回的结果类型。</typeparam>
    /// <param name="operation">异步执行当前过程计划的终结操作。</param>
    /// <returns>表示包含终结结果及输出参数访问器的过程结果的异步操作。</returns>
    private async Task<SqlProcedureResult<TExecutionResult>> ExecuteAsync<TExecutionResult>(
        Func<SqlQueryPlan, Task<TExecutionResult>> operation)
    {
        var receiver = new OutputParametersReceiver();
        var result = await operation(CreateExecutionPlan(receiver)).ConfigureAwait(false);
        return new SqlProcedureResult<TExecutionResult>(result, receiver.OutputParameters);
    }

    /// <summary>
    /// 创建附带本次输出参数接收器的过程执行计划。
    /// </summary>
    /// <param name="receiver">接收本次参数绑定结果的对象。</param>
    /// <returns>过程执行计划。</returns>
    private SqlQueryPlan CreateExecutionPlan(OutputParametersReceiver receiver)
    {
        var plan = GetPlan();
        plan.SetOutputParametersReceiver(receiver.Set, receiver.CreateSnapshot);
        return plan;
    }

    /// <summary>
    /// 保存单次过程执行绑定的输出参数访问器。
    /// </summary>
    private sealed class OutputParametersReceiver
    {
        /// <summary>
        /// 本次过程执行绑定的输出参数访问器。
        /// </summary>
        public ISqlOutputParameterAccessor OutputParameters { get; private set; }

        /// <summary>
        /// 设置本次过程执行绑定的输出参数访问器。
        /// </summary>
        /// <param name="outputParameters">由参数绑定器创建的输出参数访问器。</param>
        public void Set(ISqlOutputParameterAccessor outputParameters) => OutputParameters = outputParameters;

        /// <summary>
        /// 复制本次过程执行完成后的输出参数值。
        /// </summary>
        public void CreateSnapshot() => OutputParameters = SqlOutputParameterSnapshot.Create(OutputParameters);
    }

    /// <inheritdoc />
    private SqlQueryPlan GetPlan() => SqlQueryPlan.Create(Procedure, Parameters,
        commandType: System.Data.CommandType.StoredProcedure);
}