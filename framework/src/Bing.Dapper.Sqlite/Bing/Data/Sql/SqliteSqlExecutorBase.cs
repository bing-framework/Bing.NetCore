using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sqlite Sql执行器
/// </summary>
public abstract class SqliteSqlExecutorBase : SqlExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="SqliteSqlExecutorBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    protected SqliteSqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new SqliteBuilder(
        ServiceProvider.GetService<IEntityMetadata>(),
        ServiceProvider.GetService<ITableDatabase>(),
        null,
        ServiceProvider.GetService<IEntityMappingResolver>(),
        ServiceProvider.GetService<IDatabaseContextAccessor>(),
        ServiceProvider.GetService<ISqlParameterFactory>(),
        ServiceProvider.GetService<SqlMetadataOptions>(),
        Options,
        ServiceProvider.GetService<ISqlDatabaseContextResolver>());
}
