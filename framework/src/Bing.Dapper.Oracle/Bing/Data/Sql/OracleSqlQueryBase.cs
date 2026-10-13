using System;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle SQL 查询对象基类。
/// </summary>
public abstract class OracleSqlQueryBase : SqlQueryBase
{
    /// <summary>
    /// 初始化一个 <see cref="OracleSqlQueryBase"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析查询依赖项的服务提供程序。</param>
    /// <param name="options">当前查询的连接和执行选项。</param>
    protected OracleSqlQueryBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    protected override ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(OracleSqlProvider.Instance);
}
