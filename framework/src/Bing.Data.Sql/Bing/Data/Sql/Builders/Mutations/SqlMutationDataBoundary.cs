using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 为结构化实体写操作应用已注册的数据边界 Contributor。
/// </summary>
/// <remarks>
/// 原始表名没有实体语义，框架不会推断或改写调用方 SQL。Contributor 仅在结构化 Update/Delete
/// 的独立渲染快照中执行，使 SQL 文本和参数始终来自同一冻结 Builder。
/// </remarks>
internal static class SqlMutationDataBoundary
{
    /// <summary>
    /// 应用当前结构化写入目标的全部数据边界。
    /// </summary>
    /// <param name="context">Mutation 运行上下文。</param>
    /// <param name="table">结构化写入目标。</param>
    /// <param name="operation">当前写入操作类型。</param>
    /// <param name="whereClause">接收边界谓词的 Where 子句。</param>
    /// <returns>至少一个 Contributor 追加谓词时返回 <see langword="true"/>。</returns>
    public static bool Apply(SqlMutationContext context, SqlTableReference table, SqlDataBoundaryOperation operation,
        IMutationWhereClause whereClause)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (table?.EntityType == null || whereClause == null)
            return false;
        var boundaryContext = new SqlDataBoundaryContext(context, table, operation, whereClause);
        var applied = false;
        foreach (var contributor in GetContributors(context))
        {
            if (contributor.ShouldApply(boundaryContext) == false)
                continue;
            contributor.Apply(boundaryContext);
            applied = true;
        }
        return applied;
    }

    /// <summary>
    /// 判断当前结构化 Mutation 目标是否存在启用的数据边界。
    /// </summary>
    /// <param name="context">Mutation 运行上下文。</param>
    /// <param name="table">结构化写入目标。</param>
    /// <param name="operation">当前写入操作类型。</param>
    /// <returns>需要在独立渲染快照中追加边界时返回 <see langword="true"/>。</returns>
    public static bool ShouldApply(SqlMutationContext context, SqlTableReference table, SqlDataBoundaryOperation operation)
    {
        if (context == null || table?.EntityType == null)
            return false;
        var probe = new SqlDataBoundaryContext(context, table, operation, new ProbeMutationWhereClause());
        return GetContributors(context).Any(contributor => contributor.ShouldApply(probe));
    }

    /// <summary>
    /// 判断批量 Update 是否必须退回结构化逐实体路径以应用数据边界。
    /// </summary>
    /// <param name="context">当前 Mutation 上下文。</param>
    /// <param name="table">结构化 Update 目标。</param>
    /// <returns>存在启用的 Update 数据边界时返回 <see langword="true"/>。</returns>
    internal static bool RequiresStructuredUpdate(SqlMutationContext context, SqlTableReference table) =>
        ShouldApply(context, table, SqlDataBoundaryOperation.Update);

    /// <summary>
    /// 获取当前 Builder 注册的写入数据边界 Contributor。
    /// </summary>
    /// <param name="context">Mutation 运行上下文。</param>
    /// <returns>按服务注册顺序排列的 Contributor。</returns>
    private static IEnumerable<ISqlDataBoundaryContributor> GetContributors(SqlMutationContext context) =>
        context.Services.Filters.OfType<ISqlDataBoundaryContributor>();

    /// <summary>
    /// 用于 ShouldApply 探测的无副作用 Where 子句。
    /// </summary>
    private sealed class ProbeMutationWhereClause : IMutationWhereClause
    {
        /// <inheritdoc />
        public bool IsEmpty => true;

        /// <inheritdoc />
        public void And(ICondition condition)
        {
        }

        /// <inheritdoc />
        public void Or(ICondition condition)
        {
        }

        /// <inheritdoc />
        public IMutationWhereClause Clone(SqlMutationContext context) => new ProbeMutationWhereClause();

        /// <inheritdoc />
        public void Clear()
        {
        }

        /// <inheritdoc />
        public void AppendTo(System.Text.StringBuilder builder)
        {
        }

        /// <inheritdoc />
        /// <returns>空字符串，因为探测子句不生成实际 SQL。</returns>
        public string ToSql() => string.Empty;

        /// <inheritdoc />
        public void Validate(SqlValidationContext context)
        {
        }
    }
}
