using Dapper;

namespace Bing.Data.Sql;

// Sql查询对象 - 执行
public abstract partial class SqlQueryBase
{
    #region ExecuteScalar(获取单值)

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public object ExecuteScalar(int? timeout = null) =>
        InternalQuery((conn, sql, param, transaction) => conn.ExecuteScalar(sql, param, transaction, timeout));

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public T ExecuteScalar<T>(int? timeout = null) =>
        InternalQuery((conn, sql, param, transaction) => conn.ExecuteScalar<T>(sql, param, transaction, timeout));

    #endregion

    #region ExecuteScalarAsync(获取单值)

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public async Task<object> ExecuteScalarAsync(int? timeout = null) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => await conn.ExecuteScalarAsync(sql, param, transaction, timeout));

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public async Task<T> ExecuteScalarAsync<T>(int? timeout = null) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => await conn.ExecuteScalarAsync<T>(sql, param, transaction, timeout));

    #endregion

    #region ExecuteProcedureScalar(执行存储过程获取单值)

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public object ExecuteProcedureScalar(string procedure, int? timeout = null)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public T ExecuteProcedureScalar<T>(string procedure, int? timeout = null)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region ExecuteProcedureScalarAsync(执行存储过程获取单值)

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public async Task<object> ExecuteProcedureScalarAsync(string procedure, int? timeout = null)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public async Task<T> ExecuteProcedureScalarAsync<T>(string procedure, int? timeout = null)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region ExecuteSingle(获取单个实体)

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public TEntity ExecuteSingle<TEntity>(int? timeout = null) =>
        InternalQuery((conn, sql, param, transaction) => conn.QueryFirstOrDefault<TEntity>(sql, param, transaction, timeout));

    #endregion

    #region ExecuteSingleAsync(获取单个实体)

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public async Task<TEntity> ExecuteSingleAsync<TEntity>(int? timeout = null) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => await conn.QueryFirstOrDefaultAsync<TEntity>(sql, param, transaction, timeout));

    #endregion

    #region ExecuteProcedureSingle(执行存储过程获取单个实体)

    /// <summary>
    /// 执行存储过程获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public TEntity ExecuteProcedureSingle<TEntity>(string procedure, int? timeout = null)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region ExecuteProcedureSingleAsync(执行存储过程获取单个实体)

    /// <summary>
    /// 执行存储过程获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public async Task<TEntity> ExecuteProcedureSingleAsync<TEntity>(string procedure, int? timeout = null)
    {
        throw new NotImplementedException();
    }

    #endregion
}
