using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// MySql Sql查询对象 
/// </summary>
public abstract class MySqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected MySqlQueryBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(MySqlSqlProvider.Instance);
}
