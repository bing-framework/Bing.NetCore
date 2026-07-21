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
    protected SqliteSqlQueryBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new SqliteBuilder(
        entityMappingResolver: EntityMappingResolver,
        databaseContextAccessor: ServiceProvider.GetService<IDatabaseContextAccessor>(),
        sqlParameterFactory: ServiceProvider.GetService<ISqlParameterFactory>(),
        metadataOptions: ServiceProvider.GetService<SqlMetadataOptions>(),
        options: Options,
        databaseContextResolver: ServiceProvider.GetService<ISqlDatabaseContextResolver>(),
        objectNameFormatter: ServiceProvider.GetService<ISqlObjectNameFormatter>(),
        crossDatabaseQueryValidator: ServiceProvider.GetService<ISqlCrossDatabaseQueryValidator>(),
        tableReferenceValidator: ServiceProvider.GetService<ISqlTableReferenceValidator>());
}
