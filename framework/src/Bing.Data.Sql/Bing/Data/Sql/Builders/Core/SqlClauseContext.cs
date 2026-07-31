using Bing.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL 子句运行上下文。
/// </summary>
/// <remarks>
/// 上下文描述子句绑定到某个 Builder 时使用的运行服务，不保存 Select、Join、Where、分页或其他查询状态。
/// </remarks>
public sealed record SqlClauseContext
{
    /// <summary>
    /// 当前 SQL Builder。
    /// </summary>
    public ISqlBuilder Builder { get; }

    /// <summary>
    /// SQL 提供程序。
    /// </summary>
    public ISqlProvider Provider { get; }

    /// <summary>
    /// SQL 方言。
    /// </summary>
    public IDialect Dialect => Provider.Dialect;

    /// <summary>
    /// 当前实体解析器。
    /// </summary>
    public IEntityResolver EntityResolver { get; }

    /// <summary>
    /// 当前实体别名注册器。
    /// </summary>
    public IEntityAliasRegister AliasRegister { get; }

    /// <summary>
    /// 当前参数管理器。
    /// </summary>
    public IParameterManager ParameterManager { get; }

    /// <summary>
    /// Builder 生命周期内固定的执行上下文。
    /// </summary>
    public SqlBuilderExecutionContext ExecutionContext { get; }

    /// <summary>
    /// SQL Builder 共享服务集合。
    /// </summary>
    public SqlBuilderServices Services { get; }

    /// <summary>
    /// 初始化一个 <see cref="SqlClauseContext"/> 类型的实例。
    /// </summary>
    /// <param name="builder">当前 SQL Builder。</param>
    /// <param name="provider">SQL 提供程序。</param>
    /// <param name="entityResolver">当前实体解析器。</param>
    /// <param name="aliasRegister">当前实体别名注册器。</param>
    /// <param name="parameterManager">当前参数管理器。</param>
    /// <param name="executionContext">固定执行上下文。</param>
    /// <param name="services">SQL Builder 共享服务集合。</param>
    internal SqlClauseContext(ISqlBuilder builder, ISqlProvider provider, IEntityResolver entityResolver,
        IEntityAliasRegister aliasRegister, IParameterManager parameterManager,
        SqlBuilderExecutionContext executionContext, SqlBuilderServices services)
    {
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        EntityResolver = entityResolver ?? throw new ArgumentNullException(nameof(entityResolver));
        AliasRegister = aliasRegister ?? throw new ArgumentNullException(nameof(aliasRegister));
        ParameterManager = parameterManager ?? throw new ArgumentNullException(nameof(parameterManager));
        ExecutionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 在修改查询 Clause 前验证统一 Builder 的操作状态。
    /// </summary>
    /// <param name="action">当前查询行为。</param>
    internal void UseOperation(SqlOperationAction action) =>
        (Builder as ISqlOperationStateManager)?.UseOperation(action);

}