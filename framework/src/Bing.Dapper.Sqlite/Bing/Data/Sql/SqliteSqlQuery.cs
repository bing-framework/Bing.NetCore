namespace Bing.Data.Sql;

/// <summary>
/// Sqlite Sql查询对象
/// </summary>
public class SqliteSqlQuery : SqliteSqlQueryBase
{
    /// <inheritdoc />
    public SqliteSqlQuery(IServiceProvider serviceProvider, IDatabase database, SqlOptions sqlOptions = null) 
        : base(serviceProvider, database, sqlOptions)
    {
    }
}
