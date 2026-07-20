namespace Bing.Data.Sql;

/// <summary>
/// Sql Server Sql查询对象
/// </summary>
public class SqlServerSqlQuery : SqlServerSqlQueryBase
{
    /// <inheritdoc />
    public SqlServerSqlQuery(IServiceProvider serviceProvider, SqlOptions<SqlServerSqlQuery> options)
        : base(serviceProvider, options)
    {
    }
}
