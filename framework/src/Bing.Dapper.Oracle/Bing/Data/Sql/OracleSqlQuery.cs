using System;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle Sql查询对象
/// </summary>
public class OracleSqlQuery : OracleSqlQueryBase
{
    /// <inheritdoc />
    public OracleSqlQuery(IServiceProvider serviceProvider, SqlOptions<OracleSqlQuery> options, IDatabase database = null)
        : base(serviceProvider, options, database)
    {
    }
}
