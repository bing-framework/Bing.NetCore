using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// SqlServer Sql执行器
/// </summary>
public abstract class SqlServerSqlExecutorBase : SqlExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="SqlServerSqlExecutorBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    protected SqlServerSqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new SqlServerBuilder(new SqlBuilderServices(
        ServiceProvider.GetService<IEntityMappingResolver>(), ServiceProvider.GetService<IDatabaseContextAccessor>(),
        ServiceProvider.GetService<ISqlParameterFactory>(), ServiceProvider.GetService<SqlMetadataOptions>(), Options,
        ServiceProvider.GetService<ISqlDatabaseContextResolver>(), ServiceProvider.GetService<ISqlObjectNameFormatter>(),
        ServiceProvider.GetService<ISqlCrossDatabaseQueryValidator>(), ServiceProvider.GetService<ISqlTableReferenceValidator>()));
}
