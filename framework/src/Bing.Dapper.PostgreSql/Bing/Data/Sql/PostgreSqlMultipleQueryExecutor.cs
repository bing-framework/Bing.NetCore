using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// PostgreSQL 多结果集查询执行器。
/// </summary>
public sealed class PostgreSqlMultipleQueryExecutor : PostgreSqlMultipleQueryExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="PostgreSqlMultipleQueryExecutor"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="options">当前执行器配置。</param>
    public PostgreSqlMultipleQueryExecutor(IServiceProvider serviceProvider,
        SqlOptions<PostgreSqlMultipleQueryExecutor> options)
        : base(serviceProvider, options)
    {
    }
}