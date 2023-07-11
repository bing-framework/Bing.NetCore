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
    /// <param name="database">数据库</param>
    public MySqlExecutor(IServiceProvider serviceProvider, SqlOptions<MySqlExecutor> options, IDatabase database = null)
        : base(serviceProvider, options, database)
    {
    }
}
