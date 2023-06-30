using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 查询对象扩展
/// </summary>
public static partial class SqlQueryExtensions
{
    #region ToPagerList(获取分页列表)

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static PagerList<TReturn> ToPagerList<T1, T2, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        sqlQuery.PagerQuery(() => sqlQuery.ToList(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static PagerList<TReturn> ToPagerList<T1, T2, T3, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        sqlQuery.PagerQuery(() => sqlQuery.ToList(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="T4">参数类型4</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static PagerList<TReturn> ToPagerList<T1, T2, T3, T4, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, T4, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        sqlQuery.PagerQuery(() => sqlQuery.ToList(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="T4">参数类型4</typeparam>
    /// <typeparam name="T5">参数类型5</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static PagerList<TReturn> ToPagerList<T1, T2, T3, T4, T5, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, T4, T5, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        sqlQuery.PagerQuery(() => sqlQuery.ToList(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="T4">参数类型4</typeparam>
    /// <typeparam name="T5">参数类型5</typeparam>
    /// <typeparam name="T6">参数类型6</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static PagerList<TReturn> ToPagerList<T1, T2, T3, T4, T5, T6, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, T4, T5, T6, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        sqlQuery.PagerQuery(() => sqlQuery.ToList(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="T4">参数类型4</typeparam>
    /// <typeparam name="T5">参数类型5</typeparam>
    /// <typeparam name="T6">参数类型6</typeparam>
    /// <typeparam name="T7">参数类型7</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static PagerList<TReturn> ToPagerList<T1, T2, T3, T4, T5, T6, T7, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, T4, T5, T6, T7, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        sqlQuery.PagerQuery(() => sqlQuery.ToList(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static async Task<PagerList<TReturn>> ToPagerListAsync<T1, T2, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        await sqlQuery.PagerQueryAsync(async () => await sqlQuery.ToListAsync(map), parameter,
            connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static async Task<PagerList<TReturn>> ToPagerListAsync<T1, T2, T3, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        await sqlQuery.PagerQueryAsync(async () => await sqlQuery.ToListAsync(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="T4">参数类型4</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static async Task<PagerList<TReturn>> ToPagerListAsync<T1, T2, T3, T4, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, T4, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        await sqlQuery.PagerQueryAsync(async () => await sqlQuery.ToListAsync(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="T4">参数类型4</typeparam>
    /// <typeparam name="T5">参数类型5</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static async Task<PagerList<TReturn>> ToPagerListAsync<T1, T2, T3, T4, T5, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, T4, T5, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        await sqlQuery.PagerQueryAsync(async () => await sqlQuery.ToListAsync(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="T4">参数类型4</typeparam>
    /// <typeparam name="T5">参数类型5</typeparam>
    /// <typeparam name="T6">参数类型6</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static async Task<PagerList<TReturn>> ToPagerListAsync<T1, T2, T3, T4, T5, T6, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, T4, T5, T6, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        await sqlQuery.PagerQueryAsync(async () => await sqlQuery.ToListAsync(map), parameter, connection);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="T1">参数类型1</typeparam>
    /// <typeparam name="T2">参数类型2</typeparam>
    /// <typeparam name="T3">参数类型3</typeparam>
    /// <typeparam name="T4">参数类型4</typeparam>
    /// <typeparam name="T5">参数类型5</typeparam>
    /// <typeparam name="T6">参数类型6</typeparam>
    /// <typeparam name="T7">参数类型7</typeparam>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="map">映射操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    public static async Task<PagerList<TReturn>> ToPagerListAsync<T1, T2, T3, T4, T5, T6, T7, TReturn>(this ISqlQuery sqlQuery,
        Func<T1, T2, T3, T4, T5, T6, T7, TReturn> map, IPager parameter = null, IDbConnection connection = null) =>
        await sqlQuery.PagerQueryAsync(async () => await sqlQuery.ToListAsync(map), parameter, connection);

    #endregion

    #region ToScalar(获取单值)

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public static object ToScalar(this ISqlQuery sqlQuery, int? timeout = null) => sqlQuery.ExecuteScalar(timeout);

    #endregion

    #region ToScalarAsync(获取单值)

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public static Task<object> ToScalarAsync(this ISqlQuery sqlQuery, int? timeout = null) =>
        sqlQuery.ExecuteScalarAsync(timeout);

    #endregion

    #region To(获取单个实体)

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">返回结果类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public static TEntity To<TEntity>(this ISqlQuery sqlQuery, int? timeout = null) =>
        sqlQuery.ExecuteSingle<TEntity>(timeout);

    #endregion

    #region ToAsync(获取单个实体)

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">返回结果类型</typeparam>
    /// <param name="sqlQuery">Sql查询对象</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public static Task<TEntity> ToAsync<TEntity>(this ISqlQuery sqlQuery, int? timeout = null) =>
        sqlQuery.ExecuteSingleAsync<TEntity>(timeout);

    #endregion
}
