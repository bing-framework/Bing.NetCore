using System;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle Sql查询对象
/// </summary>
public class OracleSqlQuery : OracleSqlQueryBase
{
    /// <summary>
    /// 初始化一个 <see cref="OracleSqlQuery"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于获取查询所需服务的服务提供者。</param>
    /// <param name="options">当前 SQL 查询类型的配置项。</param>
    public OracleSqlQuery(IServiceProvider serviceProvider, SqlOptions<OracleSqlQuery> options)
        : base(serviceProvider, options)
    {
    }
}
