using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// 公用表表达式CTE操作扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 设置公用表表达式CTE
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="name">公用表达式CTE的名称</param>
    /// <param name="builder">Sql生成器</param>
    public static T With<T>(this T source, string name, ISqlBuilder builder) where T : ICte
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(name) || builder == null)
            return source;
        if (GetOperationBuilder(source) is ICteAccessor accessor)
        {
            var item = new BuilderItem(name, builder.Clone());
            SqlQueryOperationAccessor.MutateBuilder(source, _ => accessor.CteItems.Add(item));
        }
        return source;
    }

    /// <summary>
    /// 使用 Fluent 查询描述设置公用表表达式。
    /// </summary>
    /// <typeparam name="T">支持 CTE 的源类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="name">CTE 名称。</param>
    /// <param name="query">作为 CTE 使用的独立查询描述。</param>
    /// <returns>追加 CTE 后的源对象。</returns>
    public static T With<T>(this T source, string name, SqlFluentQuery query) where T : ICte =>
        With(source, name, GetQueryBuilder(query, nameof(query)));
}
