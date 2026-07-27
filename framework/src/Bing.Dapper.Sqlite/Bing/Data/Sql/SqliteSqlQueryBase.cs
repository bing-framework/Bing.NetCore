using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
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
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(SqliteSqlProvider.Instance);
}
