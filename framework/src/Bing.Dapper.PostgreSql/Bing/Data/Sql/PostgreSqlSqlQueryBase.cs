using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Matedatas;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql Sql查询对象
/// </summary>
public abstract class PostgreSqlSqlQueryBase : SqlQueryBase
{
    /// <inheritdoc />
    protected PostgreSqlSqlQueryBase(IServiceProvider serviceProvider, IDatabase database, SqlOptions sqlOptions = null)
        : base(serviceProvider, database, sqlOptions)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new PostgreSqlBuilder(
        ServiceProvider.GetService<IEntityMatedata>(),
        ServiceProvider.GetService<ITableDatabase>());
}
