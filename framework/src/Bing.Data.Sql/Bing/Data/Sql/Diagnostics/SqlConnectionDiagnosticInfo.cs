using Bing.Data.Enums;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>SQL 连接诊断信息。</summary>
public sealed class SqlConnectionDiagnosticInfo
{
    /// <summary>
    /// 已解析连接目标的数据库名称或等价标识。
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// 用于路由当前连接的数据源标识。
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 当前连接所使用的数据库类型。
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 当前连接的获取来源。
    /// </summary>
    public SqlConnectionSource Source { get; set; }

    /// <summary>
    /// 当前操作对连接资源承担的所有权。
    /// </summary>
    public SqlResourceOwnership Ownership { get; set; }

    /// <summary>
    /// 指示当前数据源是否配置为只读。
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// 当前操作选择数据源时使用的读取偏好。
    /// </summary>
    public SqlReadPreference ReadPreference { get; set; }
}