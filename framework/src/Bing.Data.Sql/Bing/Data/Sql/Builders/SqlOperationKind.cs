namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL Builder 当前操作类型。
/// </summary>
public enum SqlOperationKind
{
    /// <summary>
    /// 尚未确定操作类型。
    /// </summary>
    None = 0,

    /// <summary>
    /// 查询操作。
    /// </summary>
    Select = 1,

    /// <summary>
    /// Insert Values 操作。
    /// </summary>
    InsertValues = 2,

    /// <summary>
    /// Insert Select 操作。
    /// </summary>
    InsertSelect = 3,

    /// <summary>
    /// Update 操作。
    /// </summary>
    Update = 4,

    /// <summary>
    /// Delete 操作。
    /// </summary>
    Delete = 5
}