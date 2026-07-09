namespace Bing.Data.Sql;

/// <summary>
/// Sql执行类型
/// </summary>
public enum SqlExecutionKind
{
    /// <summary>
    /// 查询
    /// </summary>
    Query = 0,

    /// <summary>
    /// 执行
    /// </summary>
    Execute = 1,

    /// <summary>
    /// 标量查询
    /// </summary>
    Scalar = 2,

    /// <summary>
    /// 存储过程
    /// </summary>
    Procedure = 3,

    /// <summary>
    /// 插入
    /// </summary>
    Insert = 4,

    /// <summary>
    /// 更新
    /// </summary>
    Update = 5,

    /// <summary>
    /// 删除
    /// </summary>
    Delete = 6,

    /// <summary>
    /// 批量操作
    /// </summary>
    Batch = 7
}
