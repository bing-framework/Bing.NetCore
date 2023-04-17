namespace Bing.Data.Sql;

/// <summary>
/// Sql Server Sql查询对象
/// </summary>
public abstract class SqlServerSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected SqlServerSqlQueryBase(IServiceProvider serviceProvider, ISqlBuilder sqlBuilder, IDatabase database, SqlOptions sqlOptions = null) 
        : base(serviceProvider, sqlBuilder, database, sqlOptions)
    {
    }
}
