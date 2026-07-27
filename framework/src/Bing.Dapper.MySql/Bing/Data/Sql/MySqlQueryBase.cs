using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// MySQL SQL 查询对象基类。
/// </summary>
public abstract class MySqlQueryBase : SqlQueryBase
{
    /// <summary>
    /// 使用服务提供程序和 MySQL 查询选项初始化查询对象。
    /// </summary>
    /// <param name="serviceProvider">用于解析查询依赖项的服务提供程序。</param>
    /// <param name="options">当前查询的连接和执行选项。</param>
    protected MySqlQueryBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(MySqlSqlProvider.Instance);
}
