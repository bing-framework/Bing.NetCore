using System;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Matedatas;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle Sql查询对象
/// </summary>
public abstract class OracleSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected OracleSqlQueryBase(IServiceProvider serviceProvider, IDatabase database, SqlOptions sqlOptions = null)
        : base(serviceProvider, database, sqlOptions)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new OracleBuilder(
        ServiceProvider.GetService<IEntityMatedata>(),
        ServiceProvider.GetService<ITableDatabase>());
}
