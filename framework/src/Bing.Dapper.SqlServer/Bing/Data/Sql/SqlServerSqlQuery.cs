using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Matedatas;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sql Server Sql查询对象
/// </summary>
public class SqlServerSqlQuery : SqlServerSqlQueryBase
{
    /// <inheritdoc />
    public SqlServerSqlQuery(IServiceProvider serviceProvider, IDatabase database = null, SqlOptions sqlOptions = null)
        : base(serviceProvider, database, sqlOptions)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new SqlServerBuilder(
        ServiceProvider.GetService<IEntityMatedata>(),
        ServiceProvider.GetService<ITableDatabase>());
}
