using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
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
        EntityMetadata,
        ServiceProvider.GetService<ITableDatabase>(),
        null,
        EntityMappingResolver,
        ServiceProvider.GetService<IDatabaseContextAccessor>(),
        ServiceProvider.GetService<ISqlParameterFactory>(),
        ServiceProvider.GetService<SqlMetadataOptions>(),
        Options,
        ServiceProvider.GetService<ISqlDatabaseContextResolver>());

    /// <inheritdoc />
    [System.Obsolete("Dapper 连接创建已迁移至 ISqlDbConnectionFactoryResolver。")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    protected override IDatabaseFactory CreateDatabaseFactory() => new SqliteDatabaseFactory();
}
