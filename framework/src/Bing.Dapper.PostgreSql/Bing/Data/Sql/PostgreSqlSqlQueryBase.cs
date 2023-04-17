namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql Sql查询对象
/// </summary>
public abstract class PostgreSqlSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected PostgreSqlSqlQueryBase(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database, SqlOptions sqlOptions = null) : base(serviceProvider, sqlBuilder, database, sqlOptions)
    {
    }
}
