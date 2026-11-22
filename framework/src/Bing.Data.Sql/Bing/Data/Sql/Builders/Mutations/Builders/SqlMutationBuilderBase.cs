using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Builders;

/// <summary>
/// Mutation Builder 的共享生命周期和参数实现。
/// </summary>
public abstract class SqlMutationBuilderBase : ISqlMutationContextAccessor, IReturningClauseAccessor
{
    /// <summary>
    /// 初始化一个 <see cref="SqlMutationBuilderBase"/> 类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">可共享的 Builder 服务。</param>
    /// <param name="parameterManager">参数管理器；为空时按 Provider 创建。</param>
    /// <param name="clauseFactory">Mutation Clause Factory；为空时由 Provider 或默认实现提供。</param>
    protected SqlMutationBuilderBase(ISqlProvider provider, SqlBuilderServices services,
        IParameterManager parameterManager = null, ISqlMutationClauseFactory clauseFactory = null)
    {
        provider = provider ?? throw new ArgumentNullException(nameof(provider));
        services = services ?? throw new ArgumentNullException(nameof(services));
        parameterManager ??= provider.ParameterManagerFactory.Create(provider.Dialect);
        var databaseContext = services.DatabaseContextResolver.Resolve(services.Options);
        MutationContext = new SqlMutationContext(provider, parameterManager, services,
            new SqlBuilderExecutionContext(databaseContext));
        ClauseFactory = clauseFactory ?? (provider as ISqlMutationClauseFactoryProvider)?.MutationClauseFactory ??
            new DefaultSqlMutationClauseFactory();
        ReturningClause = ClauseFactory is ISqlReturningClauseFactory returningFactory
            ? returningFactory.CreateReturning(MutationContext) ??
              throw new InvalidOperationException("Mutation Returning Clause Factory 返回了空子句。")
            : new ReturningClause(MutationContext);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 获取当前 Mutation 上下文。
    /// </remarks>
    public SqlMutationContext MutationContext { get; }

    /// <inheritdoc />
    public IReturningClause ReturningClause { get; protected set; }

    /// <summary>
    /// 获取当前 Mutation Clause Factory。
    /// </summary>
    protected ISqlMutationClauseFactory ClauseFactory { get; }

    /// <summary>
    /// 获取当前 SQL Provider。
    /// </summary>
    protected ISqlProvider Provider => MutationContext.Provider;

    /// <summary>
    /// 获取当前参数管理器。
    /// </summary>
    protected IParameterManager ParameterManager => MutationContext.ParameterManager;

    /// <summary>
    /// 将当前参数导出为带元数据的快照。
    /// </summary>
    /// <returns>按当前命令状态导出的参数快照。</returns>
    public IReadOnlyCollection<SqlParam> GetParameters()
    {
        return GetParameters(ParameterManager);
    }

    /// <summary>
    /// 将指定参数管理器导出为带元数据的快照。
    /// </summary>
    /// <param name="parameterManager">待导出的参数管理器。</param>
    /// <returns>参数管理器当前状态的独立快照。</returns>
    internal IReadOnlyCollection<SqlParam> GetParameters(IParameterManager parameterManager)
    {
        if (parameterManager == null)
            throw new ArgumentNullException(nameof(parameterManager));
        if (parameterManager is IAdvancedParameterManager advancedManager)
            return advancedManager.GetSqlParams().Values.ToArray();
        return parameterManager.GetParams().Select(item => new SqlParam(item.Key, item.Value)).ToArray();
    }

    /// <summary>
    /// 验证当前参数数量未超过 Provider 上限。
    /// </summary>
    protected void ValidateParameterLimit()
    {
        ValidateParameterLimit(ParameterManager);
    }

    /// <summary>
    /// 验证指定参数管理器中的参数数量未超过 Provider 上限。
    /// </summary>
    /// <param name="parameterManager">待验证的参数管理器。</param>
    internal void ValidateParameterLimit(IParameterManager parameterManager)
    {
        if (parameterManager == null)
            throw new ArgumentNullException(nameof(parameterManager));
        var maximum = SqlProviderCapabilityResolver.GetProfile(Provider).Limits?.MaxParameterCount;
        if (maximum == null)
            return;
        if (parameterManager.Count > maximum.Value)
            throw new InvalidOperationException($"Provider {Provider.Key} 的参数数量不能超过 {maximum.Value}。");
    }

    /// <summary>
    /// 渲染 SQL 并导出一次可执行参数快照。
    /// </summary>
    /// <param name="render">当前 Builder 的 SQL 渲染操作。</param>
    /// <returns>可执行的 Mutation 命令快照。</returns>
    protected SqlWriteCommand BuildCommand(Func<string> render)
    {
        if (render == null)
            throw new ArgumentNullException(nameof(render));
        return new SqlWriteCommand(render(), GetParameters(), Provider, GetOperationKind(), !ReturningClause.IsEmpty);
    }

    /// <summary>
    /// 在方言指定的位置追加已验证的 Returning 子句。
    /// </summary>
    /// <param name="builder">接收 Mutation SQL 的字符串构建器。</param>
    /// <param name="validationContext">当前 Mutation 的验证上下文。</param>
    /// <param name="position">当前子句的 SQL 语法位置。</param>
    /// <param name="startIndex">本次 Mutation SQL 的缓冲区起始位置。</param>
    internal void AppendReturning(StringBuilder builder, SqlValidationContext validationContext,
        SqlReturningClausePosition position, int startIndex)
    {
        if (ReturningClause.IsEmpty ||
            ((Provider as ISqlReturningDialect)?.Position ?? SqlReturningClausePosition.End) != position)
            return;
        ReturningClause.Validate(validationContext);
        while (builder.Length > startIndex && char.IsWhiteSpace(builder[builder.Length - 1]))
            builder.Length--;
        ReturningClause.AppendTo(builder);
    }

    /// <summary>
    /// 获取当前专用 Builder 对应的不可变写入语义。
    /// </summary>
    /// <returns>专用 Builder 的操作类型。</returns>
    private SqlOperationKind GetOperationKind()
    {
        if (this is SqlInsertBuilder insert)
            return insert.IsInsertSelect ? SqlOperationKind.InsertSelect : SqlOperationKind.InsertValues;
        if (this is SqlUpdateBuilder)
            return SqlOperationKind.Update;
        if (this is SqlDeleteBuilder)
            return SqlOperationKind.Delete;
        throw new InvalidOperationException($"不支持生成 {GetType().FullName} 的 Mutation 命令。");
    }

    /// <summary>
    /// 创建 SQL 文本。
    /// </summary>
    /// <param name="append">向输出缓冲区追加 SQL 的操作。</param>
    /// <returns>当前 SQL 文本。</returns>
    protected string Render(Action<StringBuilder> append)
    {
        var builder = new StringBuilder(256);
        append(builder);
        return builder.ToString();
    }
}
