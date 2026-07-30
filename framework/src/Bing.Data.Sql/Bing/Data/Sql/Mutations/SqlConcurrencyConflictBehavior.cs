namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 指定实体 Mutation 遇到乐观并发冲突时的处理方式。
/// </summary>
public enum SqlConcurrencyConflictBehavior
{
    /// <summary>
    /// 实际受影响行数不符合预期时抛出并发异常。
    /// </summary>
    Throw = 0,

    /// <summary>
    /// 返回实际受影响行数，由调用方自行识别并发冲突。
    /// </summary>
    ReturnAffectedRows = 1
}