using Bing.Data.Sql.Builders;

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
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(MySqlSqlProvider.Instance);
}
