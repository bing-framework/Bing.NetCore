using System;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle Sql查询对象
/// </summary>
public abstract class OracleSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected OracleSqlQueryBase(IServiceProvider serviceProvider, SqlOptions options, IDatabase database)
        : base(serviceProvider, options, database)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new OracleBuilder(
        ServiceProvider.GetService<IEntityMetadata>(),
        ServiceProvider.GetService<ITableDatabase>());

    /// <inheritdoc />
    protected override IDatabaseFactory CreateDatabaseFactory() => new OracleDatabaseFactory();
}
