using System.Data;
using Bing.Data.Enums;

namespace Bing.Data;

/// <summary>
/// 配置 SQL 查询或执行操作使用的连接和 Provider 行为。
/// </summary>
public class SqlOptions
{
    /// <summary>
    /// 获取或设置数据库类型；默认值为 SQL Server。
    /// </summary>
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;

    /// <summary>
    /// 是否在 SQL 诊断消息中包含租户标识，默认为 false。
    /// </summary>
    public bool IncludeTenantIdInDiagnostics { get; set; }

    /// <summary>
    /// 获取或设置用于创建数据库连接的连接字符串。
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// 获取或设置调用方提供的数据库连接实例。
    /// </summary>
    /// <remarks>
    /// 设置该属性时，调用方负责连接实例的生命周期；未设置时由执行路径根据连接字符串和数据源配置解析连接。
    /// </remarks>
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
/// 配置指定实体类型使用的 SQL 查询或执行操作。
/// </summary>
/// <typeparam name="T">关联的实体或结果类型。</typeparam>
public class SqlOptions<T> : SqlOptions where T : class
{
}
