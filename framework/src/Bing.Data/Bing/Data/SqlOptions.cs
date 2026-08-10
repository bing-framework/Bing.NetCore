using System.Data;
using Bing.Data.Enums;

namespace Bing.Data;

/// <summary>
/// Sql配置
/// </summary>
public class SqlOptions
{
    /// <summary>
    /// 数据库类型，默认为Sql Server
    /// </summary>
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;

    /// <summary>
    /// 是否在 SQL 诊断消息中包含租户标识，默认为 false。
    /// </summary>
    public bool IncludeTenantIdInDiagnostics { get; set; }

    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// 数据库连接
    /// </summary>
    public IDbConnection Connection { get; set; }

    /// <summary>
    /// 查询语法能力覆盖配置。
    /// </summary>
    /// <remarks>
    /// 此配置优先于数据源能力配置；<see cref="SqlQueryCapabilityState.Inherit"/> 表示不覆盖前一层声明。
    /// </remarks>
    public SqlQueryCapabilities QueryCapabilities { get; set; }
}

/// <summary>
/// Sql配置
/// </summary>
/// <typeparam name="T">泛型类型</typeparam>
public class SqlOptions<T> : SqlOptions where T : class
{
}
