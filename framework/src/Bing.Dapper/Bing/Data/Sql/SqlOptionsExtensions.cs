using Bing.Data.Enums;
using Dapper;
using Dapper.Handlers;

namespace Bing.Data.Sql;

/// <summary>
/// Sql配置扩展
/// </summary>
public static partial class SqlOptionsExtensions
{
    /// <summary>
    /// 注册字符串类型处理器
    /// </summary>
    /// <param name="options">源</param>
    public static SqlOptions RegisterStringTypeHandler(this SqlOptions options)
    {
        SqlMapper.AddTypeHandler(typeof(string), new StringTypeHandler());
        return options;
    }

    /// <summary>
    /// 注册Guid类型处理器
    /// </summary>
    /// <param name="options">源</param>
    public static SqlOptions RegisterGuidTypeHandler(this SqlOptions options)
    {
        if (options.DatabaseType == DatabaseType.Oracle)
            SqlMapper.AddTypeHandler(new GuidTypeHandler());
        return options;
    }
}
