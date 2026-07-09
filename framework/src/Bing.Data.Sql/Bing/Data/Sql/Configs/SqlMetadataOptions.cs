using Bing.Data;
using Bing.Data.Enums;

namespace Bing.Data.Sql.Configs;

/// <summary>
/// Sql 元数据配置
/// </summary>
public class SqlMetadataOptions
{
    /// <summary>
    /// 默认数据库上下文
    /// </summary>
    public DatabaseContext DefaultDatabaseContext { get; set; } = new()
    {
        DbKey = ConnectionStringCollection.DefaultConnectionStringName,
        DatabaseType = DatabaseType.SqlServer,
        Role = DatabaseRole.Default
    };

    /// <summary>
    /// 是否启用严格元数据模式
    /// </summary>
    public bool StrictMetadata { get; set; }

    /// <summary>
    /// 布尔 true 的默认字符串值
    /// </summary>
    public string BoolTrueValue { get; set; } = "true";

    /// <summary>
    /// 布尔 false 的默认字符串值
    /// </summary>
    public string BoolFalseValue { get; set; } = "false";
}
