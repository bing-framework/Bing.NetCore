namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 逻辑架构命名方式
/// </summary>
public enum LogicalTableNamingMode
{
    /// <summary>
    /// 逻辑架构作为表名前缀
    /// </summary>
    Prefix = 0,

    /// <summary>
    /// 将逻辑架构作为物理架构。
    /// </summary>
    PhysicalSchema = 1,

    /// <summary>
    /// 不修改表名
    /// </summary>
    None = 2
}