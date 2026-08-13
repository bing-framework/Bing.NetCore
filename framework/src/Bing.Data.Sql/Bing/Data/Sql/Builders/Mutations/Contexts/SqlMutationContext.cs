using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations.Contexts;

/// <summary>
/// SQL 写操作子句运行上下文。
/// </summary>
public sealed class SqlMutationContext
{
    /// <summary>
    /// 统一 Builder 的操作状态管理器。
    /// </summary>
    internal ISqlOperationStateManager OperationStateManager { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="SqlMutationContext"/> 类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    /// <param name="services">可在 Builder 生命周期间共享的服务。</param>
    /// <param name="executionContext">当前 Builder 的执行上下文。</param>
    public SqlMutationContext(ISqlProvider provider, IParameterManager parameterManager,
        SqlBuilderServices services, SqlBuilderExecutionContext executionContext)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        ParameterManager = parameterManager ?? throw new ArgumentNullException(nameof(parameterManager));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        ExecutionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
    }

    /// <summary>
    /// 当前 SQL Provider。
    /// </summary>
    public ISqlProvider Provider { get; }

    /// <summary>
    /// 当前 SQL 方言。
    /// </summary>
    public IDialect Dialect => Provider.Dialect;

    /// <summary>
    /// 当前命令参数管理器。
    /// </summary>
    public IParameterManager ParameterManager { get; }

    /// <summary>
    /// Builder 生命周期间共享的服务集合。
    /// </summary>
    public SqlBuilderServices Services { get; }

    /// <summary>
    /// Builder 生命周期内固定的执行上下文。
    /// </summary>
    public SqlBuilderExecutionContext ExecutionContext { get; }

    /// <summary>
    /// 在修改 Mutation Clause 前验证统一 Builder 的操作状态。
    /// </summary>
    /// <param name="action">当前 Mutation 行为。</param>
    internal void UseOperation(SqlOperationAction action) => OperationStateManager?.UseOperation(action);

    /// <summary>
    /// 验证当前 Mutation 操作可执行，但不切换统一 Builder 状态。
    /// </summary>
    /// <param name="action">当前 Mutation 行为。</param>
    internal void ValidateOperation(SqlOperationAction action) => OperationStateManager?.ValidateOperation(action);
}