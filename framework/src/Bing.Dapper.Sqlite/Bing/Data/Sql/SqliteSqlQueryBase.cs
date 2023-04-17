using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Matedatas;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sqlite Sql查询对象
/// </summary>
public abstract class SqliteSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected SqliteSqlQueryBase(IServiceProvider serviceProvider, IDatabase database, SqlOptions sqlOptions = null)
        : base(serviceProvider, database, sqlOptions)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new SqliteBuilder(
        ServiceProvider.GetService<IEntityMatedata>(),
        ServiceProvider.GetService<ITableDatabase>());
}
