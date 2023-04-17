namespace Bing.Data.Sql;

/// <summary>
/// MySql Sql查询对象
/// </summary>
public class MySqlSqlQuery : MySqlSqlQueryBase
{
    /// <inheritdoc />
    public MySqlSqlQuery(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database, SqlOptions sqlOptions = null)
        : base(serviceProvider, sqlBuilder, database, sqlOptions)
    {
    }
}
