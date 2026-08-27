using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// 联合操作扩展
/// </summary>
public static partial class Extensions
{
    #region Union(联合多个查询，Union会排除重复结果行)

    /// <summary>
    /// 联合多个查询。Union会排除重复结果行
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器列表</param>
    public static T Union<T>(this T source, params ISqlBuilder[] builders)
        where T : IUnion
    {
        Union(source, "Union", builders);
        return source;
    }

    /// <summary>
    /// 联合操作
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="operation">操作方式</param>
    /// <param name="builders">Sql生成器集合</param>
    private static void Union<T>(T source, string operation, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (builders == null)
            return;
        if (GetOperationBuilder(source) is not IUnionAccessor accessor)
            return;
        var items = new List<BuilderItem>();
        foreach (var builder in builders)
        {
            if (builder == null)
                continue;
            var unionBuilder = builder.Clone();
            unionBuilder.ClearOrderBy();
            unionBuilder.ClearPageParams();
            items.Add(new BuilderItem(operation, unionBuilder));
        }
        if (items.Count == 0)
            return;
        SqlQueryOperationAccessor.MutateBuilder(source, _ => accessor.UnionItems.AddRange(items));
    }

    /// <summary>
    /// 联合多个查询。Union会排除重复结果行
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器集合</param>
    public static T Union<T>(this T source, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        Union(source, "Union", builders);
        return source;
    }

    /// <summary>
    /// 使用 Fluent 查询描述执行 Union。
    /// </summary>
    /// <typeparam name="T">支持联合操作的源类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="queries">要参与 Union 的查询描述。</param>
    /// <returns>追加联合项后的源对象。</returns>
    public static T Union<T>(this T source, params SqlFluentQuery[] queries) where T : IUnion =>
        Union(source, GetQueryBuilders(queries));

    #endregion

    #region UnionAll(联合多个查询，Union All不会排除重复结果行)

    /// <summary>
    /// 联合多个查询。Union All不会排除重复结果行
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器列表</param>
    public static T UnionAll<T>(this T source, params ISqlBuilder[] builders)
        where T : IUnion
    {
        Union(source, "Union All", builders);
        return source;
    }

    /// <summary>
    /// 联合多个查询。Union All不会排除重复结果行
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器集合</param>
    public static T UnionAll<T>(this T source, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        Union(source, "Union All", builders);
        return source;
    }

    /// <summary>
    /// 使用 Fluent 查询描述执行 Union All。
    /// </summary>
    /// <typeparam name="T">支持联合操作的源类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="queries">要参与 Union All 的查询描述。</param>
    /// <returns>追加联合项后的源对象。</returns>
    public static T UnionAll<T>(this T source, params SqlFluentQuery[] queries) where T : IUnion =>
        UnionAll(source, GetQueryBuilders(queries));

    #endregion

    #region Intersect(交集)

    /// <summary>
    /// 交集
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器列表</param>
    public static T Intersect<T>(this T source, params ISqlBuilder[] builders)
        where T : IUnion
    {
        Union(source, "Intersect", builders);
        return source;
    }

    /// <summary>
    /// 交集
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器集合</param>
    public static T Intersect<T>(this T source, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        Union(source, "Intersect", builders);
        return source;
    }

    /// <summary>
    /// 使用 Fluent 查询描述执行 Intersect。
    /// </summary>
    /// <typeparam name="T">支持联合操作的源类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="queries">要参与 Intersect 的查询描述。</param>
    /// <returns>追加联合项后的源对象。</returns>
    public static T Intersect<T>(this T source, params SqlFluentQuery[] queries) where T : IUnion =>
        Intersect(source, GetQueryBuilders(queries));

    #endregion

    #region Except(并集)

    /// <summary>
    /// 差集
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器列表</param>
    public static T Except<T>(this T source, params ISqlBuilder[] builders)
        where T : IUnion
    {
        Union(source, "Except", builders);
        return source;
    }

    /// <summary>
    /// 差集
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器集合</param>
    public static T Except<T>(this T source, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        Union(source, "Except", builders);
        return source;
    }

    /// <summary>
    /// 使用 Fluent 查询描述执行 Except。
    /// </summary>
    /// <typeparam name="T">支持联合操作的源类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="queries">要参与 Except 的查询描述。</param>
    /// <returns>追加联合项后的源对象。</returns>
    public static T Except<T>(this T source, params SqlFluentQuery[] queries) where T : IUnion =>
        Except(source, GetQueryBuilders(queries));

    /// <summary>
    /// 将查询描述转换为其专属 Builder 列表。
    /// </summary>
    /// <param name="queries">查询描述列表。</param>
    /// <returns>与查询描述一一对应的 Builder 列表。</returns>
    private static IEnumerable<ISqlBuilder> GetQueryBuilders(IEnumerable<SqlFluentQuery> queries) =>
        queries?.Select(query => GetQueryBuilder(query, nameof(queries)));

    #endregion
}
