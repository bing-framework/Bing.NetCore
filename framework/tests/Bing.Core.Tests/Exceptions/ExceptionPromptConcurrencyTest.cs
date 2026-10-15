using Bing.Exceptions.Prompts;

namespace Bing.Tests.Exceptions;

/// <summary>
/// 验证异常提示组件的并发注册行为。
/// </summary>
public sealed class ExceptionPromptConcurrencyTest
{
    /// <summary>
    /// 验证同一个异常提示实例并发注册后只会执行一次。
    /// </summary>
    [Fact]
    public void AddPrompt_WhenCalledConcurrently_ShouldKeepOneRegistration()
    {
        var prompt = new CountingPrompt();
        try
        {
            Parallel.For(0, 256, _ => ExceptionPrompt.AddPrompt(prompt));

            ExceptionPrompt.GetPrompt(new MarkerException(), isProduction: false)
                .ShouldBe("marker");
            prompt.InvocationCount.ShouldBe(1);
        }
        finally
        {
            ExceptionPrompt.RemovePrompt(prompt);
        }
    }

    /// <summary>
    /// 用于验证异常提示调用次数的异常提示实现。
    /// </summary>
    private sealed class CountingPrompt : IExceptionPrompt
    {
        /// <summary>
        /// 获取异常提示调用次数。
        /// </summary>
        public int InvocationCount => Volatile.Read(ref _invocationCount);

        /// <summary>
        /// 保存异常提示调用次数。
        /// </summary>
        private int _invocationCount;

        /// <inheritdoc />
        public string GetPrompt(Exception exception)
        {
            if (exception is not MarkerException)
                return string.Empty;

            Interlocked.Increment(ref _invocationCount);
            return "marker";
        }

        /// <inheritdoc />
        public Exception GetRawException(Exception exception) => exception;
    }

    /// <summary>
    /// 用于匹配测试提示的异常类型。
    /// </summary>
    private sealed class MarkerException : Exception
    {
    }
}
