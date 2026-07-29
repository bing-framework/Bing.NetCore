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
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object> ExecuteScalarAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => await conn.ExecuteScalarAsync(
            new CommandDefinition(sql, param, transaction, timeout, cancellationToken: cancellationToken)));

    /// <summary>
    /// 获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<T> ExecuteScalarAsync<T>(int? timeout = null, CancellationToken cancellationToken = default) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => await conn.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, param, transaction, timeout, cancellationToken: cancellationToken)));

    #endregion

    #region ExecuteProcedureScalar(执行存储过程获取单值)

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public object ExecuteProcedureScalar(string procedure, int? timeout = null)
    {
        return InternalProcedureQuery(procedure,
            (conn, command, param, transaction) => conn.ExecuteScalar(command, param, transaction, timeout,
                GetProcedureCommandType()));
    }

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public T ExecuteProcedureScalar<T>(string procedure, int? timeout = null)
    {
        return InternalProcedureQuery(procedure,
            (conn, command, param, transaction) => conn.ExecuteScalar<T>(command, param, transaction, timeout,
                GetProcedureCommandType()));
    }

    #endregion

    #region ExecuteProcedureScalarAsync(执行存储过程获取单值)

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<object> ExecuteProcedureScalarAsync(string procedure, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return InternalProcedureQueryAsync(procedure,
            async (conn, command, param, transaction) => await conn.ExecuteScalarAsync(new CommandDefinition(command,
                param, transaction, timeout, GetProcedureCommandType(), cancellationToken: cancellationToken)));
    }

    /// <summary>
    /// 执行存储过程获取单值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<T> ExecuteProcedureScalarAsync<T>(string procedure, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return InternalProcedureQueryAsync(procedure,
            async (conn, command, param, transaction) => await conn.ExecuteScalarAsync<T>(new CommandDefinition(command,
                param, transaction, timeout, GetProcedureCommandType(), cancellationToken: cancellationToken)));
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
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TEntity> ExecuteSingleAsync<TEntity>(int? timeout = null,
        CancellationToken cancellationToken = default) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => await conn.QueryFirstOrDefaultAsync<TEntity>(
            new CommandDefinition(sql, param, transaction, timeout, cancellationToken: cancellationToken)));

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
        return InternalProcedureQuery(procedure,
            (conn, command, param, transaction) => conn.QueryFirstOrDefault<TEntity>(command, param, transaction,
                timeout, GetProcedureCommandType()));
    }

    #endregion

    #region ExecuteProcedureSingleAsync(执行存储过程获取单个实体)

    /// <summary>
    /// 执行存储过程获取单个实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<TEntity> ExecuteProcedureSingleAsync<TEntity>(string procedure, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return InternalProcedureQueryAsync(procedure,
            async (conn, command, param, transaction) => await conn.QueryFirstOrDefaultAsync<TEntity>(
                new CommandDefinition(command, param, transaction, timeout, GetProcedureCommandType(),
                    cancellationToken: cancellationToken)));
    }

    #endregion
}
