using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql Sql执行器
/// </summary>
public abstract class PostgreSqlExecutorBase : SqlExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="PostgreSqlExecutorBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    /// <param name="database">数据库</param>
    protected PostgreSqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options, IDatabase database) 
        : base(serviceProvider, options, database)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new PostgreSqlBuilder(
        ServiceProvider.GetService<IEntityMetadata>(),
        ServiceProvider.GetService<ITableDatabase>());

    /// <inheritdoc />
    protected override IDatabaseFactory CreateDatabaseFactory() => new PostgreSqlDatabaseFactory();
}
