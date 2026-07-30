using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Builders;

/// <summary>
/// Mutation Builder 的共享生命周期和参数实现。
/// </summary>
public abstract class SqlMutationBuilderBase : ISqlMutationContextAccessor
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
    }

    /// <summary>
    /// 当前 Mutation 上下文。
    /// </summary>
    public SqlMutationContext MutationContext { get; }

    /// <summary>
    /// 当前 Mutation Clause Factory。
    /// </summary>
    protected ISqlMutationClauseFactory ClauseFactory { get; }

    /// <summary>
    /// 当前 SQL Provider。
    /// </summary>
    protected ISqlProvider Provider => MutationContext.Provider;

    /// <summary>
    /// 当前参数管理器。
    /// </summary>
    protected IParameterManager ParameterManager => MutationContext.ParameterManager;

    /// <summary>
    /// 将当前参数导出为带元数据的快照。
    /// </summary>
    /// <returns>按当前命令状态导出的参数快照。</returns>
    public IReadOnlyCollection<SqlParam> GetParameters()
    {
        if (ParameterManager is IAdvancedParameterManager advancedManager)
            return advancedManager.GetSqlParams().Values.ToArray();
        return ParameterManager.GetParams().Select(item => new SqlParam(item.Key, item.Value)).ToArray();
    }

    /// <summary>
    /// 验证当前参数数量未超过 Provider 上限。
    /// </summary>
    protected void ValidateParameterLimit()
    {
        if (Provider is not ISqlParameterLimitProvider { MaxParameterCount: int maximum })
            return;
        if (ParameterManager.Count > maximum)
            throw new InvalidOperationException($"Provider {Provider.Key} 的参数数量不能超过 {maximum}。");
    }

    /// <summary>
    /// 渲染 SQL 并导出一次可执行参数快照。
    /// </summary>
    /// <param name="render">当前 Builder 的 SQL 渲染操作。</param>
    /// <returns>可执行的 Mutation 命令快照。</returns>
    protected SqlMutationCommand BuildCommand(Func<string> render)
    {
        if (render == null)
            throw new ArgumentNullException(nameof(render));
        var sql = render();
        return new SqlMutationCommand(sql, GetParameters());
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