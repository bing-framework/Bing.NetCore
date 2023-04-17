using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Matedatas;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// MySql Sql查询对象 
/// </summary>
public abstract class MySqlSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected MySqlSqlQueryBase(IServiceProvider serviceProvider, IDatabase database, SqlOptions sqlOptions = null)
        : base(serviceProvider, database, sqlOptions)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new MySqlBuilder(
        ServiceProvider.GetService<IEntityMatedata>(),
        ServiceProvider.GetService<ITableDatabase>());
}
