namespace Bing.Data.Sql;

/// <summary>
/// SqlServer Sql执行器
/// </summary>
public class SqlServerSqlExecutor : SqlServerSqlExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="SqlServerSqlExecutor"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    /// <param name="database">数据库</param>
    public SqlServerSqlExecutor(IServiceProvider serviceProvider, SqlOptions options, IDatabase database)
        : base(serviceProvider, options, database)
    {
    }
}
