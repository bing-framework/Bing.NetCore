namespace Bing.Data.Sql.Tests.XUnitHelpers;

/// <summary>
/// 断言操作
/// </summary>
public class AssertHelper
{
    /// <summary>
    /// 抛出异常，并从异常消息中搜索特定关键字
    /// </summary>
    /// <typeparam name="TException">异常类型</typeparam>
    /// <param name="action">操作</param>
    /// <param name="keyword">关键字</param>
    /// <returns>返回断言捕获的指定类型异常。</returns>
    public static TException Throws<TException>(Action action, string keyword = "") where TException : Exception
    {
        var exception = Assert.Throws<TException>(action);
        if (!string.IsNullOrWhiteSpace(keyword))
            Assert.Contains(keyword, exception.Message);
        return exception;
    }

    /// <summary>
    /// 抛出异常，并从异常消息中搜索特定关键字
    /// </summary>
    /// <typeparam name="TException">异常类型</typeparam>
    /// <param name="action">操作</param>
    /// <param name="keyword">关键字</param>
    /// <returns>返回表示异步断言结果的任务，其结果为捕获的异常。</returns>
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string keyword = "") where TException : Exception
    {
        var exception = await Assert.ThrowsAsync<TException>(action);
        if (!string.IsNullOrWhiteSpace(keyword))
            Assert.Contains(keyword, exception.Message);
        return exception;
    }
}
