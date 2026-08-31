using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Extensions;

/// <summary>
/// 实体解析器扩展
/// </summary>
public static class EntityResolverExtensions
{
    /// <summary>
    /// 获取结构化表引用
    /// </summary>
    /// <param name="resolver">实体解析器</param>
    /// <param name="entity">实体类型</param>
    /// <returns>实体对应的结构化表引用。</returns>
    public static SqlTableReference GetTableReference(this IEntityResolver resolver, Type entity)
    {
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));
        if (resolver is EntityResolver entityResolver)
            return entityResolver.GetTableReference(entity);
        var table = resolver.GetTable(entity);
        return new SqlTableReference
        {
            TableName = table,
            Schema = resolver.GetSchema(entity)
        };
    }

    /// <summary>
    /// 获取表，带架构
    /// </summary>
    /// <param name="resolver">实体解析器</param>
    /// <param name="entity">实体类型</param>
    /// <returns>包含架构和表名的文本；未配置架构时仅返回表名。</returns>
    public static string GetTableAndSchema(this IEntityResolver resolver, Type entity)
    {
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));
        var schema = resolver.GetSchema(entity);
        var table = resolver.GetTable(entity);
        if (string.IsNullOrWhiteSpace(schema))
            return table;
        return $"{schema}.{table}";
    }

    /// <summary>
    /// 获取别名，如果别名为空，返回表名
    /// </summary>
    /// <param name="resolver">实体解析器</param>
    /// <param name="entity">实体类型</param>
    /// <param name="alias">别名</param>
    /// <returns>指定别名；别名为空时返回实体表名。</returns>
    public static string GetAlias(this IEntityResolver resolver, Type entity, string alias)
    {
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));
        var table = resolver.GetTable(entity);
        if (string.IsNullOrWhiteSpace(alias))
            return table;
        return alias;
    }
}