using System.Runtime.ExceptionServices;

namespace Bing.Data.Sql;

/// <summary>
/// 查询计划执行期间的异常聚合帮助器。
/// </summary>
internal static class SqlQueryPlanLifecycle
{
    /// <summary>
    /// 捕获查询计划生命周期清理期间的异常，避免其覆盖首个执行异常。
    /// </summary>
    /// <param name="cleanupExceptions">已捕获的清理异常集合。</param>
    /// <param name="operation">待执行的清理操作。</param>
    internal static void CaptureCleanupException(ICollection<Exception> cleanupExceptions, Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            cleanupExceptions.Add(exception);
        }
    }

    /// <summary>
    /// 按主异常优先、清理异常随后发生的顺序抛出查询计划生命周期异常。
    /// </summary>
    /// <param name="primaryException">执行路径中的首个异常。</param>
    /// <param name="cleanupExceptions">生命周期清理期间产生的异常。</param>
    internal static void ThrowExceptions(Exception primaryException, IReadOnlyCollection<Exception> cleanupExceptions)
    {
        if (primaryException == null && cleanupExceptions.Count == 0)
            return;
        if (primaryException == null && cleanupExceptions.Count == 1)
            ExceptionDispatchInfo.Capture(cleanupExceptions.First()).Throw();
        if (primaryException != null && cleanupExceptions.Count == 0)
            ExceptionDispatchInfo.Capture(primaryException).Throw();
        var exceptions = new List<Exception>();
        if (primaryException != null)
            exceptions.Add(primaryException);
        exceptions.AddRange(cleanupExceptions);
        throw new AggregateException(exceptions);
    }
}