namespace Bing.Data.Sql;

/// <summary>
/// Sql Server Sql查询对象
/// </summary>
public class SqlServerSqlQuery : SqlServerSqlQueryBase
{
    /// <inheritdoc />
    public SqlServerSqlQuery(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database = null, SqlOptions sqlOptions = null)
        : base(serviceProvider, sqlBuilder, database, sqlOptions)
    {
    }
}
