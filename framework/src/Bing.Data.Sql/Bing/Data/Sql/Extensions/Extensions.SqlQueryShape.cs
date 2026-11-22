// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>将查询形状模板接入原生 SQL 查询。</summary>
public static partial class Extensions
{
    /// <summary>按可选片段掩码和参数槽位创建独立的文本查询，终结调用时才执行。</summary>
    /// <param name="source">负责连接、事务及执行的根查询。</param>
    /// <param name="template">可供多个查询共享的不可变模板。</param>
    /// <param name="optionalMask">本次启用的可选片段掩码。</param>
    /// <param name="values">按所选形状中逻辑参数首次出现顺序提供的值；数量必须与槽位数一致。</param>
    /// <returns>保存本次参数快照的文本查询。</returns>
    /// <remarks>根查询的文本入口需支持 SqlParam 参数序列，不保证传入容器的具体类型。</remarks>
    public static SqlTextQuery Sql(this ISqlQuery source, SqlQueryShapeTemplate template,
        int optionalMask, ReadOnlySpan<object> values) =>
        Sql(source, template, new SqlQueryShapeKey { OptionalMask = optionalMask }, values);

    /// <summary>按结构键绑定模板，可同时为本次查询添加参数作用域。</summary>
    /// <param name="source">负责连接、事务及执行的根查询。</param>
    /// <param name="template">可供多个查询共享的不可变模板。</param>
    /// <param name="shape">隔离查询、Provider、映射版本、路由和集合形状的结构键。</param>
    /// <param name="values">按所选形状中逻辑参数首次出现顺序提供的值；null 表示显式空值。</param>
    /// <param name="scope">本次查询的参数命名作用域，可为空。</param>
    /// <returns>保存本次参数快照的文本查询。</returns>
    /// <remarks>根查询的文本入口需支持 SqlParam 参数序列，不保证传入容器的具体类型。</remarks>
    public static SqlTextQuery Sql(this ISqlQuery source, SqlQueryShapeTemplate template,
        SqlQueryShapeKey shape, ReadOnlySpan<object> values, SqlQueryScope scope = null)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        var fragment = template.RenderValues(shape, values, scope);
        // 渲染结果属于本次调用；文本查询仍会建立快照，无需经过公开属性再复制一遍。
        return source.Sql(fragment.Sql, fragment.TemplateParameters);
    }
}
