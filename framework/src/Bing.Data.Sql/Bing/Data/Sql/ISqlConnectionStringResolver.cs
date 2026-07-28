namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据源连接字符串解析器。
/// </summary>
public interface ISqlConnectionStringResolver
{
    /// <summary>
    /// 解析指定数据源的连接字符串。
    /// </summary>
    /// <param name="dataSource">SQL 数据源描述。</param>
    /// <returns>已解析的连接字符串。</returns>
    string Resolve(SqlDataSourceDescriptor dataSource);
}
