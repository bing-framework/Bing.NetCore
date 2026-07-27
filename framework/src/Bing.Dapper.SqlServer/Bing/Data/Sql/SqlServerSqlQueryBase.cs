using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// SQL Server SQL 查询对象基类。
/// </summary>
public abstract class SqlServerSqlQueryBase : SqlQueryBase
{
    /// <summary>
    /// 使用服务提供程序和 SQL Server 查询选项初始化查询对象。
    /// </summary>
    /// <param name="serviceProvider">用于解析查询依赖项的服务提供程序。</param>
    /// <param name="options">当前查询的连接和执行选项。</param>
    protected SqlServerSqlQueryBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(SqlServerSqlProvider.Instance);
}
