namespace Bing.Data.Sql;

/// <summary>
/// Sqlite Sql查询对象
/// </summary>
public class SqliteSqlQuery : SqliteSqlQueryBase
{
    /// <inheritdoc />
    public SqliteSqlQuery(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database, SqlOptions sqlOptions = null) 
        : base(serviceProvider, sqlBuilder, database, sqlOptions)
    {
    }
}
