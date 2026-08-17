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
    /// 该值在 <see cref="Metadata.DefaultEntityMappingResolver"/> 创建时固定，负值无效。达到容量后由
    /// <see cref="EntityMappingCacheEvictionPolicy"/> 决定旁路或淘汰行为。
    /// </remarks>
    public int? EntityMappingCacheCapacity { get; set; }

    /// <summary>
    /// 实体最终映射缓存达到容量后的淘汰策略。
    /// </summary>
    /// <remarks>
    /// 默认值保留既有已缓存路由，并旁路新路由。设置为 <see cref="EntityMappingCacheEvictionPolicy.LeastRecentlyUsed"/>
    /// 时，正容量缓存会淘汰最近最少使用的路由以接纳新路由。
    /// </remarks>
    public EntityMappingCacheEvictionPolicy EntityMappingCacheEvictionPolicy { get; set; } =
        EntityMappingCacheEvictionPolicy.AdmissionOnly;

    /// <summary>
    /// 实体 Mutation Plan 缓存容量。
    /// </summary>
    /// <remarks>
    /// 未设置时保持无上限缓存行为；设置为 <c>0</c> 时不缓存计划；正数表示每个实体映射解析器分区中
    /// 可保留的最大 Plan 项数，达到容量后按最近最少使用策略淘汰。
    /// </remarks>
    public int? MutationPlanCacheCapacity { get; set; }

    /// <summary>
    /// 实体 Mutation 属性 Getter 缓存容量。
    /// </summary>
    /// <remarks>
    /// 未设置时保持无上限缓存行为；设置为 <c>0</c> 时不缓存编译 Getter；正数表示每个实体映射解析器分区中
    /// 可保留的最大 Getter 项数，达到容量后按最近最少使用策略淘汰。
    /// </remarks>
    public int? MutationGetterCacheCapacity { get; set; }

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
