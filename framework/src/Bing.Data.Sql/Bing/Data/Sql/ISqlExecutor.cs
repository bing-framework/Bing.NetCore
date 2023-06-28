using System.Data;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象
/// </summary>
public interface ISqlExecutor : ISqlQuery, ISqlOperation
{
    /// <summary>
    /// 执行Sql增删改操作
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>受影响行数</returns>
    int ExecuteSql(int? timeout = null);

    /// <summary>
    /// 执行Sql增删改操作
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>受影响行数</returns>
    Task<int> ExecuteSqlAsync(int? timeout = null);

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    int ExecuteProcedure(string procedure, int? timeout = null);

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    Task<int> ExecuteProcedureAsync(string procedure, int? timeout = null);
}
