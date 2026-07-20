namespace Bing.Data.Sql;

/// <summary>
/// MySql Sql执行器
/// </summary>
public class MySqlExecutor : MySqlExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="MySqlExecutor"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    public MySqlExecutor(IServiceProvider serviceProvider, SqlOptions<MySqlExecutor> options)
        : base(serviceProvider, options)
    {
    }
}
