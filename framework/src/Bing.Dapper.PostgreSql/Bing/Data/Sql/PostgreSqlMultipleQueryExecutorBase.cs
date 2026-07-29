using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// PostgreSQL 多结果集查询执行器基类。
/// </summary>
public abstract class PostgreSqlMultipleQueryExecutorBase : SqlMultipleQueryExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="PostgreSqlMultipleQueryExecutorBase"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="options">当前执行器配置。</param>
    protected PostgreSqlMultipleQueryExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options, PostgreSqlSqlProvider.Instance.Capabilities)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(PostgreSqlSqlProvider.Instance);
}