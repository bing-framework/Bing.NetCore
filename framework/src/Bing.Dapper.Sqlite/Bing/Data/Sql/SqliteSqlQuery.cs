namespace Bing.Data.Sql;

/// <summary>
/// Sqlite Sql查询对象
/// </summary>
public class SqliteSqlQuery : SqliteSqlQueryBase
{
    /// <inheritdoc />
    public SqliteSqlQuery(IServiceProvider serviceProvider, SqlOptions<SqliteSqlQuery> options)
        : base(serviceProvider, options)
    {
    }
}
