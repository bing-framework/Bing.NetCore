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
    /// 实体最终映射缓存容量。
    /// </summary>
    /// <remarks>
    /// 未设置时保持无上限缓存行为；设置为 <c>0</c> 时不缓存最终映射；正数表示允许保留的最大映射项数。
    /// 该值在 <see cref="Metadata.DefaultEntityMappingResolver"/> 创建时固定，负值无效。
    /// </remarks>
    public int? EntityMappingCacheCapacity { get; set; }

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
