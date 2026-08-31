namespace Bing.Data.Sql;

/// <summary>
/// 标识 SQL 执行入口的操作类型，用于构造执行描述和诊断信息。
/// </summary>
public enum SqlExecutionKind
{
    /// <summary>
    /// 返回实体或行集合的查询。
    /// </summary>
    Query = 0,

    /// <summary>
    /// 不以结果集为主要返回值的通用 SQL 执行。
    /// </summary>
    Execute = 1,

    /// <summary>
    /// 返回单个标量值的查询。
    /// </summary>
    Scalar = 2,

    /// <summary>
    /// 调用存储过程的执行。
    /// </summary>
    Procedure = 3,

    /// <summary>
    /// 插入数据的变更操作。
    /// </summary>
    Insert = 4,

    /// <summary>
    /// 更新数据的变更操作。
    /// </summary>
    Update = 5,

    /// <summary>
    /// 删除数据的变更操作。
    /// </summary>
    Delete = 6,

    /// <summary>
    /// 对多条数据或多个命令进行批量处理的操作。
    /// </summary>
    Batch = 7
}
