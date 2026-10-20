namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql Sql查询对象
/// </summary>
public class PostgreSqlQuery : PostgreSqlQueryBase
{
    /// <summary>
    /// 初始化一个 <see cref="PostgreSqlQuery"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于获取查询所需服务的服务提供者。</param>
    /// <param name="options">当前 SQL 查询类型的配置项。</param>
    public PostgreSqlQuery(IServiceProvider serviceProvider, SqlOptions<PostgreSqlQuery> options)
        : base(serviceProvider, options)
    {
    }
}
