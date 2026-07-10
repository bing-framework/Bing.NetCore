namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据源解析器
/// </summary>
public interface ISqlDataSourceResolver
{
    /// <summary>
    /// 解析数据源
    /// </summary>
    /// <param name="dbKey">数据库标识</param>
    /// <param name="options">作用域选项</param>
    /// <returns>SQL 数据源描述信息</returns>
    SqlDataSourceDescriptor Resolve(string dbKey = null, DatabaseScopeOptions options = null);
}