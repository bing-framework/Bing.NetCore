using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// MySql Sql查询对象 
/// </summary>
public abstract class MySqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected MySqlQueryBase(IServiceProvider serviceProvider, SqlOptions options, IDatabase database)
        : base(serviceProvider, options, database)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new MySqlBuilder(
        ServiceProvider.GetService<IEntityMetadata>(),
        ServiceProvider.GetService<ITableDatabase>(),
        null,
        ServiceProvider.GetService<IEntityMappingResolver>(),
        ServiceProvider.GetService<IDatabaseContextAccessor>(),
        ServiceProvider.GetService<ISqlParameterFactory>(),
        ServiceProvider.GetService<SqlMetadataOptions>());

    /// <inheritdoc />
    protected override IDatabaseFactory CreateDatabaseFactory() => new MySqlDatabaseFactory();
}
