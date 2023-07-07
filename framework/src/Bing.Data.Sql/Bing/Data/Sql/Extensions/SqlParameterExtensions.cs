using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Params;
using Bing.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Sql参数(<see cref="ISqlParameter"/>) 扩展
/// </summary>
public static class SqlParameterExtensions
{
    #region AddParam(添加Sql参数)

    /// <summary>
    /// 添加Sql参数
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    public static T AddParam<T>(this T source, string name, object value = null)
        where T : ISqlParameter
    {
        source.CheckNull(nameof(source));
        if (source is ISqlPartAccessor accessor)
            accessor.ParameterManager.AddSqlParam(name, value);
        return source;
    }

    #endregion

    #region GetParams(获取参数列表)

    /// <summary>
    /// 获取参数列表
    /// </summary>
    /// <param name="source">源</param>
    public static IReadOnlyDictionary<string, object> GetParams(this ISqlParameter source)
    {
        source.CheckNull(nameof(source));
        if (source is ISqlPartAccessor accessor)
            return accessor.ParameterManager.GetParams();
        return default;
    }

    /// <summary>
    /// 获取参数列表
    /// </summary>
    /// <param name="source">源</param>
    public static IReadOnlyList<SqlParam> GetSqlParams(this ISqlParameter source)
    {
        source.CheckNull(nameof(source));
        if (source is ISqlPartAccessor accessor)
            return accessor.ParameterManager.GetSqlParams();
        return default;
    }

    #endregion

    #region GetParam(获取参数值)

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    public static T GetParam<T>(this ISqlParameter source, string name)
    {
        source.CheckNull(nameof(source));
        if (source is IGetParameter target)
            return target.GetParam<T>(name);
        if (source is ISqlPartAccessor accessor)
            return (T)accessor.ParameterManager.GetValue(name);
        return default;
    }

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    public static object GetParam(this ISqlParameter source, string name)
    {
        source.CheckNull(nameof(source));
        if (source is ISqlPartAccessor accessor)
            return accessor.ParameterManager.GetValue(name);
        return default;
    }

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    public static T GetParamValue<T>(this ISqlParameter source, string name)
    {
        source.CheckNull(nameof(source));
        if (source is IGetParameter target)
            return target.GetParam<T>(name);
        if (source is ISqlPartAccessor accessor)
            return (T)accessor.ParameterManager.GetParamValue(name);
        return default;
    }

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="name">参数名</param>
    public static object GetParamValue(this ISqlParameter source, string name)
    {
        source.CheckNull(nameof(source));
        if (source is ISqlPartAccessor accessor)
            return accessor.ParameterManager.GetParamValue(name);
        return default;
    }

    #endregion

    #region ClearParams(清空Sql参数)

    /// <summary>
    /// 清空Sql参数
    /// </summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <param name="source">源</param>
    public static T ClearParams<T>(this T source)
        where T : ISqlParameter
    {
        source.CheckNull(nameof(source));
        if (source is IClearParameters target)
        {
            target.ClearParams();
            return source;
        }
        if (source is ISqlPartAccessor accessor)
            accessor.ParameterManager.Clear();
        return source;
    }

    #endregion
}
