using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Matedatas;
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
    /// <param name="database">数据库</param>
    protected MySqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options, IDatabase database)
        : base(serviceProvider, options, database)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => new MySqlBuilder(
        ServiceProvider.GetService<IEntityMatedata>(),
        ServiceProvider.GetService<ITableDatabase>());

    /// <inheritdoc />
    protected override IDatabaseFactory CreateDatabaseFactory() => new MySqlDatabaseFactory();
}
