namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL Builder 当前操作类型。
/// </summary>
public enum SqlOperationKind
{
    /// <summary>
    /// 尚未确定 Builder 的 SQL 操作类型。
    /// </summary>
    None = 0,

    /// <summary>
    /// Select 查询操作。
    /// </summary>
    Select = 1,

    /// <summary>
    /// 使用 Values 子句插入数据的操作。
    /// </summary>
    InsertValues = 2,

    /// <summary>
    /// 使用 Select 结果插入数据的操作。
    /// </summary>
    InsertSelect = 3,

    /// <summary>
    /// 更新数据的操作。
    /// </summary>
    Update = 4,

    /// <summary>
    /// 删除数据的操作。
    /// </summary>
    Delete = 5
}