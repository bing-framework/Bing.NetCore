namespace Bing.Data.Sql;

/// <summary>
/// Sqlite Sql查询对象
/// </summary>
public abstract class SqliteSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected SqliteSqlQueryBase(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database, SqlOptions sqlOptions = null)
        : base(serviceProvider, sqlBuilder, database, sqlOptions)
    {
    }
}
