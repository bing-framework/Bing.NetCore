namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql Sql执行器
/// </summary>
public class PostgreSqlExecutor : PostgreSqlExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="PostgreSqlExecutor"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    /// <param name="database">数据库</param>
    public PostgreSqlExecutor(IServiceProvider serviceProvider, SqlOptions<PostgreSqlExecutor> options, IDatabase database) 
        : base(serviceProvider, options, database)
    {
    }
}
