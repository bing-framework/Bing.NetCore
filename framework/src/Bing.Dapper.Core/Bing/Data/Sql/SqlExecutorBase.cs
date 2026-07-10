using Bing.Data.Sql.Diagnostics;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象基类
/// </summary>
public abstract class SqlExecutorBase : SqlQueryBase, ISqlExecutor
{
    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="SqlExecutorBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    /// <param name="database">数据库</param>
    protected SqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options, IDatabase database)
        : base(serviceProvider, options, database)
    {
    }

    #endregion

    #region ExecuteSql(执行Sql增删改操作)

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual int ExecuteSql(string sql, object param = null, int? timeout = null)
    {
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetConnection();
            var dbParameters = GetDbParameters(param);
            var parameterMetadata = GetSqlParameterDiagnostics(param);
            message = ExecuteBefore(sql, param, connection, dbParameters, parameterMetadata);
            result = connection.Execute(sql, dbParameters, GetTransaction(), timeout);
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }

    #endregion

    #region ExecuteSqlAsync(执行Sql增删改操作)

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual async Task<int> ExecuteSqlAsync(string sql, object param = null, int? timeout = null)
    {
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetConnection();
            var dbParameters = GetDbParameters(param);
            var parameterMetadata = GetSqlParameterDiagnostics(param);
            message = ExecuteBefore(sql, param, connection, dbParameters, parameterMetadata);
            result = await connection.ExecuteAsync(sql, dbParameters, GetTransaction(), timeout);
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }

    #endregion

    #region ExecuteProcedure(执行存储过程增删改操作)

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual int ExecuteProcedure(string procedure, object param = null, int? timeout = null)
    {
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetConnection();
            var dbParameters = GetDbParameters(param);
            var parameterMetadata = GetSqlParameterDiagnostics(param);
            message = ExecuteBefore(procedure, param, connection, dbParameters, parameterMetadata);
            result = connection.Execute(procedure, dbParameters, GetTransaction(), timeout, GetProcedureCommandType());
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }

    #endregion

    #region ExecuteProcedureAsync(执行存储过程增删改操作)

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual async Task<int> ExecuteProcedureAsync(string procedure, object param = null, int? timeout = null)
    {
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetConnection();
            var dbParameters = GetDbParameters(param);
            var parameterMetadata = GetSqlParameterDiagnostics(param);
            message = ExecuteBefore(procedure, param, connection, dbParameters, parameterMetadata);
            result = await connection.ExecuteAsync(procedure, dbParameters, GetTransaction(), timeout, GetProcedureCommandType());
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">Sql 参数映射</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual int ExecuteSql<TEntity>(string sql, SqlParameterMap<TEntity> param, int? timeout = null)
        where TEntity : class => ExecuteSql(sql, (object)param, timeout);

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">Sql 参数映射</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual Task<int> ExecuteSqlAsync<TEntity>(string sql, SqlParameterMap<TEntity> param, int? timeout = null)
        where TEntity : class => ExecuteSqlAsync(sql, (object)param, timeout);

    #endregion
}
