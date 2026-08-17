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
public sealed class TenantIdFilter : ISqlFilter, ISqlDataBoundaryContributor
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
        if (context == null || context.IsEnabled<TenantIdFilter>() == false)
            return;
        foreach (var source in context.Sources)
            ApplyQuery(context, source);
    }

    /// <inheritdoc />
    public bool ShouldApply(SqlDataBoundaryContext context) => context?.EntityType != null &&
        context.IsEnabled<TenantIdFilter>() && Contributor.IsTenantEntity(context.EntityType);

    /// <inheritdoc />
    public void Apply(SqlDataBoundaryContext context)
    {
        if (ShouldApply(context) == false)
            return;
        context.AddEquals(TenantPropertyName, GetTenantId(context.EntityType, context.DatabaseContext));
    }

    /// <summary>
    /// 为结构化来源贡献租户边界谓词。
    /// </summary>
    private void ApplyQuery(SqlFilterContext context, SqlFilterSource source)
    {
        if (source?.EntityType == null || string.IsNullOrWhiteSpace(source.Alias) ||
            Contributor.IsTenantEntity(source.EntityType) == false)
            return;
        var value = GetTenantId(source.EntityType, context.DatabaseContext);
        context.AddPredicate(source, context.GetColumn(source, TenantPropertyName, required: true), value);
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

}