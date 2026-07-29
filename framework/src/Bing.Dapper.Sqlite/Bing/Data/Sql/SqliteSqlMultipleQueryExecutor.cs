using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// SQLite 多结果集查询执行器。
/// </summary>
public sealed class SqliteSqlMultipleQueryExecutor : SqliteSqlMultipleQueryExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="SqliteSqlMultipleQueryExecutor"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="options">当前执行器配置。</param>
    public SqliteSqlMultipleQueryExecutor(IServiceProvider serviceProvider,
        SqlOptions<SqliteSqlMultipleQueryExecutor> options)
        : base(serviceProvider, options)
    {
    }
}