namespace Bing.Data.Sql;

/// <summary>
/// Sqlite Sql执行器
/// </summary>
public class SqliteSqlExecutor : SqliteSqlExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="SqliteSqlExecutor"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    /// <param name="database">数据库</param>
    public SqliteSqlExecutor(IServiceProvider serviceProvider, SqlOptions options, IDatabase database)
        : base(serviceProvider, options, database)
    {
    }
}
