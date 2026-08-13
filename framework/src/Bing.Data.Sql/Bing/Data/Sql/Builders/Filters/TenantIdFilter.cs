using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Filters;

/// <summary>
/// 为结构化租户实体追加 TenantId 数据边界。
/// </summary>
/// <remarks>
/// 筛选仅应用于 <see cref="ISqlTenantFilterContributor"/> 声明的实体。租户值缺失时会拒绝渲染，
/// 防止无租户边界的查询或写入穿透数据隔离。
/// </remarks>
public sealed class TenantIdFilter : ISqlFilter
{
    /// <summary>
    /// 租户列对应的实体属性名称。
    /// </summary>
    private const string TenantPropertyName = "TenantId";

    /// <summary>
    /// 初始化一个 <see cref="TenantIdFilter"/> 类型的实例。
    /// </summary>
    /// <param name="contributor">提供实体适用范围和当前租户值的应用层扩展点。</param>
    public TenantIdFilter(ISqlTenantFilterContributor contributor) =>
        Contributor = contributor ?? throw new ArgumentNullException(nameof(contributor));

    /// <summary>
    /// 当前租户过滤扩展点。
    /// </summary>
    public ISqlTenantFilterContributor Contributor { get; }

    /// <inheritdoc />
    public void Filter(SqlFilterContext context)
    {
        if (context == null || IsEnabled(context.DataFilter) == false)
            return;
        foreach (var source in context.RootSources)
            ApplyQuery(context, source, true);
        foreach (var source in context.JoinSources)
            ApplyQuery(context, source, false);
    }

    /// <summary>
    /// 判断结构化写入目标是否需要租户边界。
    /// </summary>
    internal bool ShouldApply(SqlMutationContext context, SqlTableReference table) => table?.EntityType != null &&
        IsEnabled(context?.Services?.DataFilter) && Contributor.IsTenantEntity(table.EntityType);

    /// <summary>
    /// 将租户边界添加到结构化 Update 或 Delete 的 Where 子句。
    /// </summary>
    internal bool ApplyMutation(SqlMutationContext context, SqlTableReference table, IMutationWhereClause whereClause)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (whereClause == null || ShouldApply(context, table) == false)
            return false;
        var column = ResolveColumn(context.Services.EntityMappingResolver, context.Services.EntityModelMetadataProvider,
            table.EntityType, context.ExecutionContext.DatabaseContext);
        var value = GetTenantId(table.EntityType, context.ExecutionContext.DatabaseContext);
        var parameter = context.Services.ParameterFactory.Create(context.ParameterManager.GenerateName(), value, column,
            context.ExecutionContext.DatabaseContext, table.EntityType, SqlParameterSource.SqlBuilder);
        AddParameter(context.ParameterManager, parameter);
        whereClause.And(SqlConditionFactory.Create(GetQualifiedColumn(context.Dialect, table.Alias, column.ColumnName),
            context.Dialect.GetParamName(parameter.Name), Operator.Equal));
        return true;
    }

    /// <summary>
    /// 将租户边界添加到根表 Where 或 Join On。
    /// </summary>
    private void ApplyQuery(SqlFilterContext context, TableSource source, bool isRoot)
    {
        if (source?.EntityType == null || string.IsNullOrWhiteSpace(source.Alias) ||
            Contributor.IsTenantEntity(source.EntityType) == false)
            return;
        var column = ResolveColumn(context.EntityMappingResolver, context.EntityModelMetadataProvider, source.EntityType,
            context.DatabaseContext);
        var value = GetTenantId(source.EntityType, context.DatabaseContext);
        var qualifiedColumn = GetQualifiedColumn(context.Dialect, source.Alias, column.ColumnName);
        if (isRoot)
        {
            context.ClauseAccessor.WhereClause.Where(qualifiedColumn, value);
            return;
        }
        (context.ClauseAccessor.JoinClause as Clauses.JoinClause)?.AddFilterCondition(source.SourceId, qualifiedColumn,
            value);
    }

    /// <summary>
    /// 获取适用实体的当前租户值，缺失时拒绝继续构建 SQL。
    /// </summary>
    private object GetTenantId(Type entityType, DatabaseContext databaseContext)
    {
        var value = Contributor.GetTenantId(new SqlTenantFilterContext(entityType, databaseContext));
        if (value == null || value is string text && string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"租户过滤已启用，但实体 {entityType.Name} 未解析到当前租户值。");
        return value;
    }

    /// <summary>
    /// 获取已映射的 TenantId 列，适用实体缺少映射时拒绝渲染。
    /// </summary>
    private static ColumnMappingMetadata ResolveColumn(IEntityMappingResolver mappingResolver,
        IEntityModelMetadataProvider modelMetadataProvider, Type entityType, DatabaseContext databaseContext)
    {
        var mapping = mappingResolver?.Resolve(entityType, databaseContext);
        var column = mapping?.Columns?.Values.FirstOrDefault(item =>
            string.Equals(item.PropertyName, TenantPropertyName, StringComparison.OrdinalIgnoreCase));
        if (column != null)
            return column;
        throw new InvalidOperationException($"租户实体 {entityType.Name} 的属性 {TenantPropertyName} 未映射到数据库列。");
    }

    /// <summary>
    /// 根据别名生成安全列标识符。
    /// </summary>
    private static string GetQualifiedColumn(IDialect dialect, string alias, string columnName) =>
        string.IsNullOrWhiteSpace(alias)
            ? dialect.SafeName(columnName)
            : $"{dialect.SafeName(alias)}.{dialect.SafeName(columnName)}";

    /// <summary>
    /// 将带元数据参数写入当前管理器。
    /// </summary>
    private static void AddParameter(IParameterManager parameterManager, SqlParam parameter)
    {
        if (parameterManager is IAdvancedParameterManager advancedParameterManager)
            advancedParameterManager.Add(parameter);
        else
            parameterManager.Add(parameter.Name, parameter.Value);
    }

    /// <summary>
    /// 判断租户过滤是否在当前异步执行流中启用。
    /// </summary>
    private bool IsEnabled(Bing.Data.Filters.IDataFilter dataFilter) => dataFilter?.IsEnabled<TenantIdFilter>() != false;
}