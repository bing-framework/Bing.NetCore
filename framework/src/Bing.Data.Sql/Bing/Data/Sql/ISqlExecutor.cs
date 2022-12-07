using System.Data;
using System.Threading.Tasks;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象
/// </summary>
public interface ISqlExecutor
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
}