using Bing.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象扩展
/// </summary>
public static class SqlExecutorExtensions
{
    /// <summary>
    /// 执行 SQL，并使用实体参数映射增强参数元数据
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="executor">Sql 执行对象</param>
    /// <param name="sql">Sql 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="map">参数映射配置</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public static int ExecuteSql<TEntity>(this ISqlExecutor executor, string sql, object param,
        Action<SqlParameterMap<TEntity>> map, int? timeout = null)
        where TEntity : class
    {
        executor.CheckNull(nameof(executor));
        if (map == null)
            return executor.ExecuteSql(sql, param, timeout);
        var parameterMap = new SqlParameterMap<TEntity>().UseSource(param);
        map(parameterMap);
        return executor.ExecuteSql(sql, parameterMap, timeout);
    }

    /// <summary>
    /// 执行 SQL，并使用实体参数映射增强参数元数据
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="executor">Sql 执行对象</param>
    /// <param name="sql">Sql 语句</param>
    /// <param name="param">参数对象</param>
    /// <param name="map">参数映射配置</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public static Task<int> ExecuteSqlAsync<TEntity>(this ISqlExecutor executor, string sql, object param,
        Action<SqlParameterMap<TEntity>> map, int? timeout = null)
        where TEntity : class
    {
        executor.CheckNull(nameof(executor));
        if (map == null)
            return executor.ExecuteSqlAsync(sql, param, timeout);
        var parameterMap = new SqlParameterMap<TEntity>().UseSource(param);
        map(parameterMap);
        return executor.ExecuteSqlAsync(sql, parameterMap, timeout);
    }
}