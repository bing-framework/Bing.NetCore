namespace Bing.Data.Sql.Configs;

/// <summary>
/// Sql 元数据配置
/// </summary>
public class SqlMetadataOptions
{
    /// <summary>
    /// SQL 数据源配置
    /// </summary>
    public SqlDataSourceOptions DataSources { get; } = new();

    /// <summary>
    /// 实体映射配置集合
    /// </summary>
    public IList<EntityMappingOptions> EntityMappings { get; } = new List<EntityMappingOptions>();

    /// <summary>
    /// 默认数据库上下文
    /// </summary>
    public DatabaseContext DefaultDatabaseContext { get; set; } = new();

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
