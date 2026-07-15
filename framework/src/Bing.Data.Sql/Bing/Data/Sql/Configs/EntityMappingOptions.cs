namespace Bing.Data.Sql.Configs;

/// <summary>
/// 实体映射配置
/// </summary>
public sealed class EntityMappingOptions
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 实体映射配置名称
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 架构
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 表路由键
    /// </summary>
    public string TableRouteKey { get; set; }

    /// <summary>
    /// 列映射配置集合
    /// </summary>
    public IDictionary<string, ColumnMappingOptions> Columns { get; } =
        new Dictionary<string, ColumnMappingOptions>(StringComparer.OrdinalIgnoreCase);
}