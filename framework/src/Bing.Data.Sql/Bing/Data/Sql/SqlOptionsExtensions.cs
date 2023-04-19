using System.Data;
using Bing.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Sql配置扩展
/// </summary>
public static partial class SqlOptionsExtensions
{
    /// <summary>
    /// 设置数据库连接字符串
    /// </summary>
    /// <param name="options">源</param>
    /// <param name="connectionString">数据库连接字符串</param>
    public static SqlOptions ConnectionString(this SqlOptions options, string connectionString)
    {
        options.CheckNull(nameof(options));
        options.ConnectionString = connectionString;
        return options;
    }

    /// <summary>
    /// 设置数据库连接
    /// </summary>
    /// <param name="options">源</param>
    /// <param name="connection">数据库连接</param>
    public static SqlOptions Connection(this SqlOptions options, IDbConnection connection)
    {
        options.CheckNull(nameof(options));
        options.Connection = connection;
        return options;
    }
}
