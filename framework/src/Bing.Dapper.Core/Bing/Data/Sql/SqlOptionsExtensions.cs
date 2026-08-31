using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Dapper.Handlers;

namespace Bing.Dapper;

/// <summary>
/// Dapper Sql 配置扩展
/// </summary>
public static class DapperSqlOptionsExtensions
{
    /// <summary>
    /// 注册字符串类型处理器
    /// </summary>
    /// <param name="options">源</param>
    /// <returns>完成类型处理器注册的 SQL 配置。</returns>
    public static SqlOptions RegisterStringTypeHandler(this SqlOptions options)
    {
        SqlMapper.AddTypeHandler(typeof(string), new StringTypeHandler());
        return options;
    }

    /// <summary>
    /// 注册Guid类型处理器
    /// </summary>
    /// <param name="options">源</param>
    /// <returns>完成类型处理器注册的 SQL 配置。</returns>
    public static SqlOptions RegisterGuidTypeHandler(this SqlOptions options)
    {
        if (options.DatabaseType == DatabaseType.Oracle)
            SqlMapper.AddTypeHandler(new GuidTypeHandler());
        return options;
    }
}
