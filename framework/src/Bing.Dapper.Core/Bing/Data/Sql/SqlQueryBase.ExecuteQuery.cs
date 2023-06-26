using Dapper;

namespace Bing.Data.Sql;

// Sql查询对象 - 执行查询
public abstract partial class SqlQueryBase
{
    #region ExecuteQuery(获取实体集合)

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<dynamic> ExecuteQuery(int? timeout = null, bool buffered = true)
    {
        List<dynamic> result = default;
        try
        {
            if (ExecuteBefore() == false)
                return default;
            var connection = GetConnection();
            result = connection.Query(GetSql(), Params, GetTransaction(), buffered, timeout).ToList();
            return result;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<TEntity>(int? timeout = null, bool buffered = true)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="T1">实体类型1</typeparam>
    /// <typeparam name="T2">实体类型2</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="map">映射函数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<T1, T2, TEntity>(Func<T1, T2, TEntity> map, int? timeout = null, bool buffered = true)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="T1">实体类型1</typeparam>
    /// <typeparam name="T2">实体类型2</typeparam>
    /// <typeparam name="T3">实体类型3</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="map">映射函数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<T1, T2, T3, TEntity>(Func<T1, T2, T3, TEntity> map, int? timeout = null, bool buffered = true)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="T1">实体类型1</typeparam>
    /// <typeparam name="T2">实体类型2</typeparam>
    /// <typeparam name="T3">实体类型3</typeparam>
    /// <typeparam name="T4">实体类型4</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="map">映射函数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<T1, T2, T3, T4, TEntity>(Func<T1, T2, T3, T4, TEntity> map, int? timeout = null, bool buffered = true)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="T1">实体类型1</typeparam>
    /// <typeparam name="T2">实体类型2</typeparam>
    /// <typeparam name="T3">实体类型3</typeparam>
    /// <typeparam name="T4">实体类型4</typeparam>
    /// <typeparam name="T5">实体类型5</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="map">映射函数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<T1, T2, T3, T4, T5, TEntity>(Func<T1, T2, T3, T4, T5, TEntity> map, int? timeout = null, bool buffered = true)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="T1">实体类型1</typeparam>
    /// <typeparam name="T2">实体类型2</typeparam>
    /// <typeparam name="T3">实体类型3</typeparam>
    /// <typeparam name="T4">实体类型4</typeparam>
    /// <typeparam name="T5">实体类型5</typeparam>
    /// <typeparam name="T6">实体类型6</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="map">映射函数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<T1, T2, T3, T4, T5, T6, TEntity>(Func<T1, T2, T3, T4, T5, T6, TEntity> map, int? timeout = null, bool buffered = true)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="T1">实体类型1</typeparam>
    /// <typeparam name="T2">实体类型2</typeparam>
    /// <typeparam name="T3">实体类型3</typeparam>
    /// <typeparam name="T4">实体类型4</typeparam>
    /// <typeparam name="T5">实体类型5</typeparam>
    /// <typeparam name="T6">实体类型6</typeparam>
    /// <typeparam name="T7">实体类型7</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="map">映射函数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<T1, T2, T3, T4, T5, T6, T7, TEntity>(Func<T1, T2, T3, T4, T5, T6, T7, TEntity> map, int? timeout = null, bool buffered = true)
    {
        throw new NotImplementedException();
    }

    #endregion

}
