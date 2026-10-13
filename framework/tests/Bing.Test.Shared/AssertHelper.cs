using System;
using System.Threading.Tasks;
using Xunit;

namespace Bing.Test.Shared;

/// <summary>
/// 测试断言辅助方法。
/// </summary>
public class AssertHelper
{
    /// <summary>
    /// 抛出异常并验证异常消息。
    /// </summary>
    /// <typeparam name="TException">异常类型。</typeparam>
    /// <param name="action">待执行的操作。</param>
    /// <param name="keyword">异常消息中应包含的关键字。</param>
    /// <returns>捕获的异常。</returns>
    public static TException Throws<TException>(Action action, string keyword = "") where TException : Exception
    {
        var exception = Assert.Throws<TException>(action);
        if (!string.IsNullOrWhiteSpace(keyword))
            Assert.Contains(keyword, exception.Message);
        return exception;
    }

    /// <summary>
    /// 异步抛出异常并验证异常消息。
    /// </summary>
    /// <typeparam name="TException">异常类型。</typeparam>
    /// <param name="action">待执行的异步操作。</param>
    /// <param name="keyword">异常消息中应包含的关键字。</param>
    /// <returns>捕获的异常。</returns>
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string keyword = "") where TException : Exception
    {
        var exception = await Assert.ThrowsAsync<TException>(action);
        if (!string.IsNullOrWhiteSpace(keyword))
            Assert.Contains(keyword, exception.Message);
        return exception;
    }
}
