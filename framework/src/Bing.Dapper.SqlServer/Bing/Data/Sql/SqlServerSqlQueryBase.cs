using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Matedatas;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sql Server Sql查询对象
/// </summary>
public abstract class SqlServerSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected SqlServerSqlQueryBase(IServiceProvider serviceProvider, SqlOptions options, IDatabase database) 
        : base(serviceProvider, options, database)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new SqlServerBuilder(
        ServiceProvider.GetService<IEntityMatedata>(),
        ServiceProvider.GetService<ITableDatabase>());

    /// <inheritdoc />
    protected override IDatabaseFactory CreateDatabaseFactory() => new SqlServerDatabaseFactory();
}
