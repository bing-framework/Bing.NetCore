namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql Sql查询对象
/// </summary>
public class PostgreSqlSqlQuery : PostgreSqlSqlQueryBase
{
    /// <inheritdoc />
    public PostgreSqlSqlQuery(IServiceProvider serviceProvider, IDatabase database, SqlOptions sqlOptions = null) 
        : base(serviceProvider, database, sqlOptions)
    {
    }
}
