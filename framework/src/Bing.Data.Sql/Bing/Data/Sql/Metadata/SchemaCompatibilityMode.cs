namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 旧架构配置兼容模式
/// </summary>
public enum SchemaCompatibilityMode
{
    /// <summary>
    /// 根据数据库 Provider 推断旧 Schema 的语义。
    /// </summary>
    Auto = 0,

    /// <summary>
    /// 将旧 Schema 解释为逻辑表名前缀
    /// </summary>
    LegacySchemaAsLogical = 1,

    /// <summary>
    /// 将旧 Schema 解释为物理架构
    /// </summary>
    LegacySchemaAsPhysical = 2
}