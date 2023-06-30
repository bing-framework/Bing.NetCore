using Bing.Data.Sql.Diagnostics;
using Dapper;

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
    /// 执行Sql增删改操作
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual int ExecuteSql(int? timeout = null)
    {
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetConnection();
            var sql = GetSql();
            message = ExecuteBefore(sql, Params, connection);
            WriteTraceLog(sql, Params, GetDebugSql());
            result = connection.Execute(sql, Params, GetTransaction(), timeout);
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
    /// 执行Sql增删改操作
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual async Task<int> ExecuteSqlAsync(int? timeout = null)
    {
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetConnection();
            var sql = GetSql();
            message = ExecuteBefore(sql, Params, connection);
            WriteTraceLog(sql, Params, GetDebugSql());
            result = await connection.ExecuteAsync(sql, Params, GetTransaction(), timeout);
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
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual int ExecuteProcedure(string procedure, int? timeout = null)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region ExecuteProcedureAsync(执行存储过程增删改操作)

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual Task<int> ExecuteProcedureAsync(string procedure, int? timeout = null)
    {
        throw new NotImplementedException();
    }

    #endregion
}
