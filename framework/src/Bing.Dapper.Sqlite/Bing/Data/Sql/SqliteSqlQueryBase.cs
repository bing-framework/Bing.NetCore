using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sqlite Sql查询对象
/// </summary>
public abstract class SqliteSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected SqliteSqlQueryBase(IServiceProvider serviceProvider, SqlOptions options, IDatabase database)
        : base(serviceProvider, options, database)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new SqliteBuilder(
        ServiceProvider.GetService<IEntityMetadata>(),
        ServiceProvider.GetService<ITableDatabase>());

    /// <inheritdoc />
    protected override IDatabaseFactory CreateDatabaseFactory() => new SqliteDatabaseFactory();
}
