namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文作用域选项
/// </summary>
public sealed class DatabaseScopeOptions
{
    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// 读取偏好
    /// </summary>
    public SqlReadPreference ReadPreference { get; set; } = SqlReadPreference.Default;
}