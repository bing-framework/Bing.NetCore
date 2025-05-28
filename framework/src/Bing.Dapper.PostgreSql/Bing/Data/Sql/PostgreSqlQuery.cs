namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql Sql查询对象
/// </summary>
public class PostgreSqlQuery : PostgreSqlQueryBase
{
    /// <inheritdoc />
    public PostgreSqlQuery(IServiceProvider serviceProvider, SqlOptions<PostgreSqlQuery> options, IDatabase database = null)
        : base(serviceProvider, options, database)
    {
    }
}
