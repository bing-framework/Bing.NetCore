namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 结构对应的执行类型。
/// </summary>
public enum SqlExecutionKind
{
    /// <summary>
    /// 查询操作。
    /// </summary>
    Query,

    /// <summary>
    /// 插入操作。
    /// </summary>
    Insert,

    /// <summary>
    /// 更新操作。
    /// </summary>
    Update,

    /// <summary>
    /// 删除操作。
    /// </summary>
    Delete
}