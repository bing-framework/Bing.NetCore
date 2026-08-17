using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// SQLite 多结果集查询执行器基类。
/// </summary>
public abstract class SqliteSqlMultipleQueryExecutorBase : SqlMultipleQueryExecutorBase
{
    /// <summary>
    /// 初始化一个<see cref="SqliteSqlMultipleQueryExecutorBase"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="options">当前执行器配置。</param>
    protected SqliteSqlMultipleQueryExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(SqliteSqlProvider.Instance);
}
