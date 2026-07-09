using Bing.Data;
using Bing.Data.Enums;

namespace Bing.Data.Sql.Configs;

/// <summary>
/// Sql 元数据配置
/// </summary>
public class SqlMetadataOptions
{
    /// <summary>
    /// 数据库描述信息集合
    /// </summary>
    public IDictionary<string, DatabaseDescriptor> Databases { get; } =
        new Dictionary<string, DatabaseDescriptor>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 实体映射配置集合
    /// </summary>
    public IList<EntityMappingOptions> EntityMappings { get; } = new List<EntityMappingOptions>();

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

    /// <summary>
    /// 获取数据库描述信息键
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="role">数据库角色</param>
    /// <returns>数据库描述信息键</returns>
    public static string GetDatabaseDescriptorKey(string dbKey, DatabaseType databaseType,
        DatabaseRole role = DatabaseRole.Default) =>
        $"{dbKey ?? string.Empty}:{databaseType}:{role}";
}
