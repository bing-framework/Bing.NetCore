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
        if (!(source is IUnionAccessor accessor))
            return;
        foreach (var builder in builders)
        {
            builder.ClearOrderBy();
            builder.ClearPageParams();
            accessor.UnionItems.Add(new BuilderItem(operation, builder));
        }
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

    #endregion
}
