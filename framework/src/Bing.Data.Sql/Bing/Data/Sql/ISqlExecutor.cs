using System.Data;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象
/// </summary>
public interface ISqlExecutor : ISqlQuery, ISqlOperation
{
    /// <summary>
    /// 设置数据库连接
    /// </summary>
    /// <param name="connection">数据库连接</param>
    ISqlExecutor SetConnection(IDbConnection connection);

    /// <summary>
    /// 执行Sql语句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数</param>
    int ExecuteSql(string sql, object param = null);

    /// <summary>
    /// 执行Sql语句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="param">参数</param>
    Task<int> ExecuteSqlAsync(string sql, object param = null);

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    Task<int> ExecuteProcedureAsync(string procedure, int? timeout = null);
}
