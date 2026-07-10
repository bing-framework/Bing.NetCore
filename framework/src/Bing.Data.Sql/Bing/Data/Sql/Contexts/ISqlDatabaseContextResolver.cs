using Bing.Data;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库上下文解析器
/// </summary>
public interface ISqlDatabaseContextResolver
{
    /// <summary>
    /// 解析当前 SQL 使用的数据库上下文
    /// </summary>
    /// <param name="options">Sql 配置</param>
    /// <returns>数据库上下文</returns>
    DatabaseContext Resolve(SqlOptions options = null);
}
