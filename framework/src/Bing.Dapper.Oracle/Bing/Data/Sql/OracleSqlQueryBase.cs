using System;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle Sql查询对象
/// </summary>
public abstract class OracleSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected OracleSqlQueryBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new OracleBuilder(new SqlBuilderServices(
        EntityMappingResolver, ServiceProvider.GetService<IDatabaseContextAccessor>(),
        ServiceProvider.GetService<ISqlParameterFactory>(), ServiceProvider.GetService<SqlMetadataOptions>(), Options,
        ServiceProvider.GetService<ISqlDatabaseContextResolver>(), ServiceProvider.GetService<ISqlObjectNameFormatter>(),
        ServiceProvider.GetService<ISqlCrossDatabaseQueryValidator>(), ServiceProvider.GetService<ISqlTableReferenceValidator>()));
}
