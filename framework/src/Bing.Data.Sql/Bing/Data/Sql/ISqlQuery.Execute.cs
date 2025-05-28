namespace Bing.Data.Sql;

// ISqlQuery - Execute
public partial interface ISqlQuery
{
    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    object ExecuteScalar(int? timeout = null);

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    Task<object> ExecuteScalarAsync(int? timeout = null);

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    T ExecuteScalar<T>(int? timeout = null);

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    Task<T> ExecuteScalarAsync<T>(int? timeout = null);

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    object ExecuteProcedureScalar(string procedure, int? timeout = null);

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    Task<object> ExecuteProcedureScalarAsync(string procedure, int? timeout = null);

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    T ExecuteProcedureScalar<T>(string procedure, int? timeout = null);

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    Task<T> ExecuteProcedureScalarAsync<T>(string procedure, int? timeout = null);

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    TEntity ExecuteSingle<TEntity>(int? timeout = null);

    /// <summary>
    /// 获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    Task<TEntity> ExecuteSingleAsync<TEntity>(int? timeout = null);

    /// <summary>
    /// 执行存储过程获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    TEntity ExecuteProcedureSingle<TEntity>(string procedure, int? timeout = null);

    /// <summary>
    /// 执行存储过程获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    Task<TEntity> ExecuteProcedureSingleAsync<TEntity>(string procedure, int? timeout = null);
}
