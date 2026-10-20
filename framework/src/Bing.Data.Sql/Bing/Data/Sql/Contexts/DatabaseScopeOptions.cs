namespace Bing.Data.Sql;

/// <summary>
/// 配置临时数据库上下文作用域的覆盖项。
/// </summary>
public sealed class DatabaseScopeOptions
{
    /// <summary>
    /// 获取或设置要使用的数据源标识。
    /// </summary>
    /// <remarks>未设置时继承父上下文或默认数据源。</remarks>
    public string DbKey { get; set; }

    /// <summary>
    /// 获取或设置要应用的租户标识。
    /// </summary>
    /// <remarks>未设置时由作用域管理器继承父上下文。</remarks>
    public string TenantId { get; set; }

    /// <summary>
    /// 获取或设置要覆盖的读取偏好。
    /// </summary>
    /// <remarks>为 <c>null</c> 时不覆盖父上下文或默认偏好。</remarks>
    public SqlReadPreference? ReadPreference { get; set; }
}
