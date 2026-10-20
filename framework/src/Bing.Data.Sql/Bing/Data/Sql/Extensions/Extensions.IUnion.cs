using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// 执行 Union 集合操作。
public static partial class Extensions
{
    #region Union(联合多个查询，Union会排除重复结果行)

    /// <summary>
    /// 执行 Union 集合操作。
    /// </summary>
    /// <remarks>
    /// Union 操作会排除重复结果行。
    /// </remarks>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器列表</param>
    /// <returns>追加 Union 项后的源对象。</returns>
    public static T Union<T>(this T source, params ISqlBuilder[] builders)
        where T : IUnion
    {
        Union(source, "Union", builders);
        return source;
    }

    /// <summary>
    /// 执行 Union 集合操作。
    /// </summary>
    /// <remarks>
    /// Union 操作会排除重复结果行。
    /// </remarks>
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
    /// 执行 Union 集合操作。
    /// </summary>
    /// <remarks>
    /// Union 操作会排除重复结果行。
    /// </remarks>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器集合</param>
    /// <returns>追加 Union 项后的源对象。</returns>
    public static T Union<T>(this T source, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        Union(source, "Union", builders);
        return source;
    }

    /// <summary>
    /// 执行 Union 集合操作。
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
    /// 执行 Union All 集合操作。
    /// </summary>
    /// <remarks>
    /// Union All 操作会保留重复结果行。
    /// </remarks>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器列表</param>
    /// <returns>追加 Union All 项后的源对象。</returns>
    public static T UnionAll<T>(this T source, params ISqlBuilder[] builders)
        where T : IUnion
    {
        Union(source, "Union All", builders);
        return source;
    }

    /// <summary>
    /// 执行 Union All 集合操作。
    /// </summary>
    /// <remarks>
    /// Union All 操作会保留重复结果行。
    /// </remarks>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器集合</param>
    /// <returns>追加 Union All 项后的源对象。</returns>
    public static T UnionAll<T>(this T source, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        Union(source, "Union All", builders);
        return source;
    }

    /// <summary>
    /// 执行 Union All 集合操作。
    /// </summary>
    /// <remarks>
    /// Union All 操作会保留重复结果行。
    /// </remarks>
    /// <typeparam name="T">支持联合操作的源类型。</typeparam>
    /// <param name="source">当前查询源。</param>
    /// <param name="queries">要参与 Union All 的查询描述。</param>
    /// <returns>追加联合项后的源对象。</returns>
    public static T UnionAll<T>(this T source, params SqlFluentQuery[] queries) where T : IUnion =>
        UnionAll(source, GetQueryBuilders(queries));

    #endregion

    #region Intersect(交集)

    /// <summary>
    /// 执行 Intersect 集合操作。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器列表</param>
    /// <returns>追加 Intersect 项后的源对象。</returns>
    public static T Intersect<T>(this T source, params ISqlBuilder[] builders)
        where T : IUnion
    {
        Union(source, "Intersect", builders);
        return source;
    }

    /// <summary>
    /// 执行 Intersect 集合操作。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器集合</param>
    /// <returns>追加 Intersect 项后的源对象。</returns>
    public static T Intersect<T>(this T source, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        Union(source, "Intersect", builders);
        return source;
    }

    /// <summary>
    /// 执行 Intersect 集合操作。
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
    /// 执行 Except 集合操作。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器列表</param>
    /// <returns>追加 Except 项后的源对象。</returns>
    public static T Except<T>(this T source, params ISqlBuilder[] builders)
        where T : IUnion
    {
        Union(source, "Except", builders);
        return source;
    }

    /// <summary>
    /// 执行 Except 集合操作。
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="builders">Sql生成器集合</param>
    /// <returns>追加 Except 项后的源对象。</returns>
    public static T Except<T>(this T source, IEnumerable<ISqlBuilder> builders)
        where T : IUnion
    {
        Union(source, "Except", builders);
        return source;
    }

    /// <summary>
    /// 执行 Except 集合操作。
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
