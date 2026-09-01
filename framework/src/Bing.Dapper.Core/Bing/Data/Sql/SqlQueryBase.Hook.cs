namespace Bing.Data.Sql;

/// <summary>
/// 提供 SQL 查询执行生命周期钩子。
/// </summary>
public partial class SqlQueryBase
{
    /// <summary>
    /// 执行前操作。
    /// </summary>
    /// <returns>允许继续执行时返回 <see langword="true"/>；返回 <see langword="false"/> 时停止执行。</returns>
    protected virtual bool ExecuteBefore() => true;

    /// <summary>
    /// 执行后操作
    /// </summary>
    /// <param name="result">结果</param>
    protected virtual void ExecuteAfter(object result)
    {
        if (Volatile.Read(ref _queryPlanExecutionDepth) == 0)
            Clear();
    }

    /// <summary>
    /// 通知独立查询描述执行结束。
    /// </summary>
    /// <param name="result">查询执行结果。</param>
    /// <remarks>
    /// 保留派生类的业务钩子调用，同时禁止基类默认行为清空 Root Builder。
    /// </remarks>
    private void ExecuteQueryPlanAfter(object result)
    {
        Interlocked.Increment(ref _queryPlanExecutionDepth);
        try
        {
            ExecuteAfter(result);
        }
        finally
        {
            Interlocked.Decrement(ref _queryPlanExecutionDepth);
        }
    }
}
