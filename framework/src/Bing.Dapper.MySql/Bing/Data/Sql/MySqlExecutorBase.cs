using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// MySql Sql执行器
/// </summary>
public abstract class MySqlExecutorBase : SqlExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="MySqlExecutorBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    protected MySqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new MySqlBuilder(
        entityMappingResolver: ServiceProvider.GetService<IEntityMappingResolver>(),
        databaseContextAccessor: ServiceProvider.GetService<IDatabaseContextAccessor>(),
        sqlParameterFactory: ServiceProvider.GetService<ISqlParameterFactory>(),
        metadataOptions: ServiceProvider.GetService<SqlMetadataOptions>(),
        options: Options,
        databaseContextResolver: ServiceProvider.GetService<ISqlDatabaseContextResolver>(),
        objectNameFormatter: ServiceProvider.GetService<ISqlObjectNameFormatter>(),
        crossDatabaseQueryValidator: ServiceProvider.GetService<ISqlCrossDatabaseQueryValidator>(),
        tableReferenceValidator: ServiceProvider.GetService<ISqlTableReferenceValidator>());
}
