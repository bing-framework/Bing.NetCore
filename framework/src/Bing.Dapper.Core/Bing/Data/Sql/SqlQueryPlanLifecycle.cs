using System.Runtime.ExceptionServices;

namespace Bing.Data.Sql;

/// <summary>
/// 查询计划执行期间的异常聚合帮助器。
/// </summary>
internal static class SqlQueryPlanLifecycle
{
    /// <summary>
    /// 在统一的主异常和清理异常语义下执行立即完成的同步操作。
    /// </summary>
    /// <typeparam name="TResult">操作结果类型。</typeparam>
    /// <param name="operation">实际执行的主操作。</param>
    /// <param name="failureCleanup">主操作失败后的清理步骤。</param>
    /// <param name="completionCleanup">无论成功或失败均执行的完成清理步骤。</param>
    /// <param name="release">最后执行的资源释放步骤。</param>
    /// <returns>主操作的执行结果。</returns>
    /// <remarks>
    /// 失败清理由调用方按其领域顺序写入异常集合；完成和释放步骤由本方法隔离，避免覆盖主异常。
    /// </remarks>
    internal static TResult Execute<TResult>(Func<TResult> operation,
        Action<Exception, ICollection<Exception>> failureCleanup, Action<TResult> completionCleanup, Action release)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (failureCleanup == null)
            throw new ArgumentNullException(nameof(failureCleanup));
        if (completionCleanup == null)
            throw new ArgumentNullException(nameof(completionCleanup));
        if (release == null)
            throw new ArgumentNullException(nameof(release));

        TResult result = default;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            result = operation();
        }
        catch (Exception exception)
        {
            primaryException = exception;
            CaptureCleanupException(cleanupExceptions, () => failureCleanup(exception, cleanupExceptions));
        }
        finally
        {
            CaptureCleanupException(cleanupExceptions, () => completionCleanup(result));
        }
        CaptureCleanupException(cleanupExceptions, release);
        ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

    /// <summary>
    /// 在统一的主异常和清理异常语义下执行立即完成的异步操作。
    /// </summary>
    /// <typeparam name="TResult">操作结果类型。</typeparam>
    /// <param name="operation">实际执行的异步主操作。</param>
    /// <param name="failureCleanup">主操作失败后的异步清理步骤。</param>
    /// <param name="completionCleanup">无论成功或失败均执行的完成清理步骤。</param>
    /// <param name="release">最后执行的资源释放步骤。</param>
    /// <returns>表示主操作执行结果的异步任务。</returns>
    /// <remarks>
    /// 失败清理由调用方按其领域顺序写入异常集合；完成和释放步骤由本方法隔离，避免覆盖主异常。
    /// </remarks>
    internal static async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation,
        Func<Exception, ICollection<Exception>, Task> failureCleanup, Action<TResult> completionCleanup, Action release)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (failureCleanup == null)
            throw new ArgumentNullException(nameof(failureCleanup));
        if (completionCleanup == null)
            throw new ArgumentNullException(nameof(completionCleanup));
        if (release == null)
            throw new ArgumentNullException(nameof(release));

        TResult result = default;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            result = await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            await CaptureCleanupExceptionAsync(cleanupExceptions, () => failureCleanup(exception, cleanupExceptions))
                .ConfigureAwait(false);
        }
        finally
        {
            CaptureCleanupException(cleanupExceptions, () => completionCleanup(result));
        }
        CaptureCleanupException(cleanupExceptions, release);
        ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

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
    /// 异步捕获查询计划生命周期清理期间的异常，避免其覆盖首个执行异常。
    /// </summary>
    /// <param name="cleanupExceptions">已捕获的清理异常集合。</param>
    /// <param name="operation">待执行的异步清理操作。</param>
    internal static async Task CaptureCleanupExceptionAsync(ICollection<Exception> cleanupExceptions,
        Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
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