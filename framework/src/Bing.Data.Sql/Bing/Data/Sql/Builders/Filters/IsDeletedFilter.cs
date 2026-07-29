using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Filters;

/// <summary>
/// 逻辑删除过滤器
/// </summary>
public class IsDeletedFilter : ISqlFilter
{
    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="context">Sql查询执行上下文</param>
    public void Filter(SqlFilterContext context)
    {
        foreach (var item in context.EntityAliasRegister.Data)
            Filter(context, item.Key, item.Value);
    }

    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="context">Sql查询执行上下文</param>
    /// <param name="type">类型</param>
    /// <param name="alias">表别名</param>
    private void Filter(SqlFilterContext context, Type type, string alias)
    {
        if (type == null)
            return;
        if (string.IsNullOrWhiteSpace(alias))
            return;
        if (typeof(ISoftDelete).IsAssignableFrom(type) == false)
            return;
        var columnName = ResolveColumn(context, type, "IsDeleted");
        var isDeleted = $"{context.Dialect.SafeName(alias)}.{context.Dialect.SafeName(columnName)}";
        if (context.EntityAliasRegister.FromType == type)
        {
            context.ClauseAccessor.WhereClause.Where(isDeleted, false);
            return;
        }
        context.ClauseAccessor.JoinClause.Find(type)?.On(isDeleted, false);
    }

    /// <summary>
    /// 解析列名
    /// </summary>
    /// <param name="context">Sql查询执行上下文</param>
    /// <param name="type">实体类型</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>列名</returns>
    private static string ResolveColumn(SqlFilterContext context, Type type, string propertyName)
    {
        var mapping = context.EntityMappingResolver?.Resolve(type, context.DatabaseContext);
        if (mapping?.Columns != null)
        {
            if (mapping.Columns.TryGetValue(propertyName, out var column))
                return column.ColumnName;
            var mappedColumn = mapping.Columns.Values.FirstOrDefault(t =>
                string.Equals(t.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));
            if (mappedColumn != null)
                return mappedColumn.ColumnName;
        }

        var model = context.EntityModelMetadataProvider?.GetMetadata(type);
        if (model?.Properties != null && model.Properties.TryGetValue(propertyName, out var property))
            return property.ColumnName;
        return propertyName;
    }
}
