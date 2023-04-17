namespace Bing.Data.Sql;

/// <summary>
/// MySql Sql查询对象 
/// </summary>
public abstract class MySqlSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected MySqlSqlQueryBase(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database, SqlOptions sqlOptions = null)
        : base(serviceProvider, sqlBuilder, database, sqlOptions)
    {
    }
}
