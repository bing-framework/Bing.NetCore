using Bing.Data.Sql.Diagnostics;

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
    public List<dynamic> ExecuteQuery(int? timeout = null, bool buffered = true) =>
        InternalQuery((conn, sql, param, transaction) => conn.Query(sql, param, transaction, buffered, timeout).ToList());

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<TEntity>(int? timeout = null, bool buffered = true) =>
        InternalQuery((conn, sql, param, transaction) => conn.Query<TEntity>(sql, param, transaction, buffered, timeout).ToList());

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="T1">实体类型1</typeparam>
    /// <typeparam name="T2">实体类型2</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="map">映射函数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public List<TEntity> ExecuteQuery<T1, T2, TEntity>(Func<T1, T2, TEntity> map, int? timeout = null, bool buffered = true) =>
        InternalQuery((conn, sql, param, transaction) => conn.Query(sql, map, param, transaction, buffered, "Id", timeout).ToList());

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
    public List<TEntity> ExecuteQuery<T1, T2, T3, TEntity>(Func<T1, T2, T3, TEntity> map, int? timeout = null, bool buffered = true) =>
        InternalQuery((conn, sql, param, transaction) => conn.Query(sql, map, param, transaction, buffered, "Id", timeout).ToList());

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
    public List<TEntity> ExecuteQuery<T1, T2, T3, T4, TEntity>(Func<T1, T2, T3, T4, TEntity> map, int? timeout = null, bool buffered = true) =>
        InternalQuery((conn, sql, param, transaction) => conn.Query(sql, map, param, transaction, buffered, "Id", timeout).ToList());

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
    public List<TEntity> ExecuteQuery<T1, T2, T3, T4, T5, TEntity>(Func<T1, T2, T3, T4, T5, TEntity> map, int? timeout = null, bool buffered = true) =>
        InternalQuery((conn, sql, param, transaction) => conn.Query(sql, map, param, transaction, buffered, "Id", timeout).ToList());

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
    public List<TEntity> ExecuteQuery<T1, T2, T3, T4, T5, T6, TEntity>(Func<T1, T2, T3, T4, T5, T6, TEntity> map, int? timeout = null, bool buffered = true) =>
        InternalQuery((conn, sql, param, transaction) => conn.Query(sql, map, param, transaction, buffered, "Id", timeout).ToList());

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
    public List<TEntity> ExecuteQuery<T1, T2, T3, T4, T5, T6, T7, TEntity>(Func<T1, T2, T3, T4, T5, T6, T7, TEntity> map, int? timeout = null, bool buffered = true) =>
        InternalQuery((conn, sql, param, transaction) => conn.Query(sql, map, param, transaction, buffered, "Id", timeout).ToList());

    #endregion

    #region ExecuteQueryAsync(获取实体集合)

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<List<dynamic>> ExecuteQueryAsync(int? timeout = null,
        bool buffered = true, CancellationToken cancellationToken = default) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) =>
            (await conn.QueryAsync(CreateQueryCommandDefinition(sql, param, transaction, timeout, buffered,
                cancellationToken))).ToList());

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<List<TEntity>> ExecuteQueryAsync<TEntity>(int? timeout = null,
        bool buffered = true, CancellationToken cancellationToken = default) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) =>
            (await conn.QueryAsync<TEntity>(CreateQueryCommandDefinition(sql, param, transaction, timeout, buffered,
                cancellationToken))).ToList());

    /// <summary>
    /// 获取实体集合
    /// </summary>
    /// <typeparam name="T1">实体类型1</typeparam>
    /// <typeparam name="T2">实体类型2</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="map">映射函数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否缓存。默认值：true</param>
    public async Task<List<TEntity>> ExecuteQueryAsync<T1, T2, TEntity>(Func<T1, T2, TEntity> map, int? timeout = null, bool buffered = true) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => (await conn.QueryAsync(sql, map, param, transaction, buffered, "Id", timeout)).ToList());

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
    public async Task<List<TEntity>> ExecuteQueryAsync<T1, T2, T3, TEntity>(Func<T1, T2, T3, TEntity> map, int? timeout = null, bool buffered = true) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => (await conn.QueryAsync(sql, map, param, transaction, buffered, "Id", timeout)).ToList());

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
    public async Task<List<TEntity>> ExecuteQueryAsync<T1, T2, T3, T4, TEntity>(Func<T1, T2, T3, T4, TEntity> map, int? timeout = null, bool buffered = true) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => (await conn.QueryAsync(sql, map, param, transaction, buffered, "Id", timeout)).ToList());

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
    public async Task<List<TEntity>> ExecuteQueryAsync<T1, T2, T3, T4, T5, TEntity>(Func<T1, T2, T3, T4, T5, TEntity> map, int? timeout = null, bool buffered = true) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => (await conn.QueryAsync(sql, map, param, transaction, buffered, "Id", timeout)).ToList());

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
    public async Task<List<TEntity>> ExecuteQueryAsync<T1, T2, T3, T4, T5, T6, TEntity>(Func<T1, T2, T3, T4, T5, T6, TEntity> map, int? timeout = null, bool buffered = true) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => (await conn.QueryAsync(sql, map, param, transaction, buffered, "Id", timeout)).ToList());

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
    public async Task<List<TEntity>> ExecuteQueryAsync<T1, T2, T3, T4, T5, T6, T7, TEntity>(Func<T1, T2, T3, T4, T5, T6, T7, TEntity> map, int? timeout = null, bool buffered = true) =>
        await InternalQueryAsync(async (conn, sql, param, transaction) => (await conn.QueryAsync(sql, map, param, transaction, buffered, "Id", timeout)).ToList());

    #endregion

    /// <summary>
    /// 创建异步列表查询命令定义
    /// </summary>
    /// <param name="sql">SQL 语句</param>
    /// <param name="parameters">Dapper 参数对象</param>
    /// <param name="transaction">数据库事务</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="buffered">是否由 Dapper 缓冲结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="commandType">命令类型</param>
    /// <returns>Dapper 命令定义</returns>
    protected virtual CommandDefinition CreateQueryCommandDefinition(string sql, object parameters,
        IDbTransaction transaction, int? timeout, bool buffered, CancellationToken cancellationToken = default,
        CommandType? commandType = null)
    {
        return new CommandDefinition(sql, parameters, transaction, timeout, commandType,
            buffered ? CommandFlags.Buffered : CommandFlags.None, cancellationToken);
    }

    /// <summary>
    /// 内部查询
    /// </summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="func">查询操作</param>
    protected TResult InternalQuery<TResult>(Func<IDbConnection, string, object, IDbTransaction, TResult> func)
    {
        TResult result = default;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return default;
            var connection = GetConnection();
            var sql = GetSql();
            var dbParameters = GetDbParameters();
            var parameterMetadata = GetSqlParameterDiagnostics(SqlBuilder);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(sql, Params, connection, parameterMetadata);
            WriteTraceLog(sql, Params, GetDebugSql());
            result = func(connection, sql, dbParameters, transaction);
            CompleteQueryTransaction();
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }

    /// <summary>
    /// 内部查询
    /// </summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="func">查询操作</param>
    protected async Task<TResult> InternalQueryAsync<TResult>(Func<IDbConnection, string, object, IDbTransaction, Task<TResult>> func)
    {
        TResult result = default;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return default;
            var connection = GetConnection();
            var sql = GetSql();
            var dbParameters = GetDbParameters();
            var parameterMetadata = GetSqlParameterDiagnostics(SqlBuilder);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(sql, Params, connection, parameterMetadata);
            WriteTraceLog(sql, Params, GetDebugSql());
            result = await func(connection, sql, dbParameters, transaction);
            CompleteQueryTransaction();
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }
}
