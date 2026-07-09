namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体映射元数据
/// </summary>
public sealed class EntityMappingMetadata
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 数据库标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public Bing.Data.Enums.DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据库角色
    /// </summary>
    public DatabaseRole Role { get; set; }

    /// <summary>
    /// 架构
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 完整表名
    /// </summary>
    public string FullTableName { get; set; }

    /// <summary>
    /// 表路由键
    /// </summary>
    public string TableRouteKey { get; set; }

    /// <summary>
    /// 映射版本
    /// </summary>
    public string MappingVersion { get; set; }

    /// <summary>
    /// 列映射集合
    /// </summary>
    public IReadOnlyDictionary<string, ColumnMappingMetadata> Columns { get; set; }
}
