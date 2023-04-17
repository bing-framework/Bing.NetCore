using System;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle Sql查询对象
/// </summary>
public class OracleSqlQuery : OracleSqlQueryBase
{
    /// <inheritdoc />
    public OracleSqlQuery(IServiceProvider serviceProvider, IDatabase database, SqlOptions sqlOptions = null) 
        : base(serviceProvider, database, sqlOptions)
    {
    }
}
