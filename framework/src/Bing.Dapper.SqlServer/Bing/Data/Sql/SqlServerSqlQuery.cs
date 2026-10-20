namespace Bing.Data.Sql;

/// <summary>
/// Sql Server Sql查询对象
/// </summary>
public class SqlServerSqlQuery : SqlServerSqlQueryBase
{
    /// <summary>
    /// 初始化一个 <see cref="SqlServerSqlQuery"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于获取查询所需服务的服务提供者。</param>
    /// <param name="options">当前 SQL 查询类型的配置项。</param>
    public SqlServerSqlQuery(IServiceProvider serviceProvider, SqlOptions<SqlServerSqlQuery> options)
        : base(serviceProvider, options)
    {
    }
}
