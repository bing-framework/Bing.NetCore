using Bing.Data;
using Bing.Data.Filters;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 为结构化实体写操作应用数据边界谓词。
/// </summary>
/// <remarks>
/// 原始表名缺少实体语义，不能安全推断过滤条件。默认边界支持逻辑删除和显式配置的租户过滤器。
/// </remarks>
internal static class SqlMutationDataBoundary
{
    /// <summary>
    /// 应用当前 Mutation 目标的默认数据边界。
    /// </summary>
    /// <param name="context">Mutation 运行上下文。</param>
    /// <param name="table">结构化目标表。</param>
    /// <param name="whereClause">待追加条件的 Mutation Where 子句。</param>
    public static bool Apply(SqlMutationContext context, SqlTableReference table, IMutationWhereClause whereClause)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (table?.EntityType == null || whereClause == null)
            return false;
        var applied = ApplySoftDelete(context, table, whereClause);
        foreach (var tenantFilter in context.Services.Filters.OfType<TenantIdFilter>())
            applied |= tenantFilter.ApplyMutation(context, table, whereClause);
        return applied;
    }

    /// <summary>
    /// 应用逻辑删除写入边界。
    /// </summary>
    private static bool ApplySoftDelete(SqlMutationContext context, SqlTableReference table,
        IMutationWhereClause whereClause)
    {
        if (ShouldApplySoftDelete(context, table) == false)
            return false;
        var mapping = context.Services.EntityMappingResolver.Resolve(table.EntityType,
            context.ExecutionContext.DatabaseContext);
        var column = mapping?.Columns?.Values.FirstOrDefault(item =>
            string.Equals(item.PropertyName, nameof(ISoftDelete.IsDeleted), StringComparison.OrdinalIgnoreCase));
        if (column == null)
            throw new InvalidOperationException($"实体 {table.EntityType.Name} 实现了 {nameof(ISoftDelete)}，但属性 {nameof(ISoftDelete.IsDeleted)} 未映射到数据库列。");

        var parameter = context.Services.ParameterFactory.Create(context.ParameterManager.GenerateName(), false, column,
            context.ExecutionContext.DatabaseContext, table.EntityType, SqlParameterSource.SqlBuilder);
        if (context.ParameterManager is IAdvancedParameterManager advancedManager)
            advancedManager.Add(parameter);
        else
            context.ParameterManager.Add(parameter.Name, parameter.Value);
        var left = string.IsNullOrWhiteSpace(table.Alias)
            ? context.Dialect.SafeName(column.ColumnName)
            : $"{context.Dialect.SafeName(table.Alias)}.{context.Dialect.SafeName(column.ColumnName)}";
        whereClause.And(SqlConditionFactory.Create(left, context.Dialect.GetParamName(parameter.Name), Operator.Equal));
        return true;
    }

    /// <summary>
    /// 判断当前结构化 Mutation 目标是否需要默认数据边界。
    /// </summary>
    /// <param name="context">Mutation 运行上下文。</param>
    /// <param name="table">结构化目标表。</param>
    /// <returns>需要在渲染快照中追加边界时返回 true。</returns>
    public static bool ShouldApply(SqlMutationContext context, SqlTableReference table) =>
        ShouldApplySoftDelete(context, table) || context?.Services?.Filters.OfType<TenantIdFilter>()
            .Any(filter => filter.ShouldApply(context, table)) == true;

    /// <summary>
    /// 判断实体是否需要通过结构化逐实体 Update 应用写入边界。
    /// </summary>
    /// <param name="services">当前执行器创建 Builder 时使用的共享服务。</param>
    /// <param name="entityType">批量 Update 的实体类型。</param>
    /// <returns>存在软删除或租户边界时返回 <c>true</c>。</returns>
    internal static bool RequiresStructuredUpdate(SqlBuilderServices services, Type entityType)
    {
        if (entityType == null)
            return false;
        if (typeof(ISoftDelete).IsAssignableFrom(entityType) && ShouldApplySoftDelete(services))
            return true;
        return services?.Filters.OfType<TenantIdFilter>().Any(filter =>
            IsTenantFilterEnabled(services.DataFilter) && filter.Contributor.IsTenantEntity(entityType)) == true;
    }

    /// <summary>
    /// 判断当前 Builder 服务是否启用默认软删除过滤。
    /// </summary>
    private static bool ShouldApplySoftDelete(SqlMutationContext context, SqlTableReference table)
    {
        if (table?.EntityType == null || typeof(ISoftDelete).IsAssignableFrom(table.EntityType) == false)
            return false;
        return ShouldApplySoftDelete(context?.Services);
    }

    /// <summary>
    /// 判断当前服务是否启用了逻辑删除过滤。
    /// </summary>
    private static bool ShouldApplySoftDelete(SqlBuilderServices services)
    {
        if (services?.Filters?.Any(filter => filter is IsDeletedFilter) == false)
            return false;
        return services.DataFilter?.IsEnabled<ISoftDelete>() != false;
    }

    /// <summary>
    /// 判断租户过滤是否在当前异步执行流中启用。
    /// </summary>
    private static bool IsTenantFilterEnabled(IDataFilter dataFilter) => dataFilter?.IsEnabled<TenantIdFilter>() != false;
}