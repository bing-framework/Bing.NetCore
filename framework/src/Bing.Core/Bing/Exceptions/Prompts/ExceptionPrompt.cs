using Bing.Extensions;
using Bing.Properties;

namespace Bing.Exceptions.Prompts;

/// <summary>
/// 异常提示
/// </summary>
public static class ExceptionPrompt
{
    /// <summary>
    /// 保护异常提示集合的同步对象。
    /// </summary>
    private static readonly object _syncRoot = new object();

    /// <summary>
    /// 异常提示组件集合
    /// </summary>
    private static readonly List<IExceptionPrompt> _prompts = new List<IExceptionPrompt>();

    /// <summary>
    /// 添加异常提示
    /// </summary>
    /// <param name="prompt">异常提示</param>
    public static void AddPrompt(IExceptionPrompt prompt)
    {
        RegisterPrompt(prompt);
    }

    /// <summary>
    /// 注册异常提示并返回可撤销的注册句柄。
    /// </summary>
    /// <param name="prompt">异常提示。</param>
    /// <returns>用于撤销本次注册的句柄。</returns>
    /// <remarks>
    /// 长期存在的框架提示可以使用 <see cref="AddPrompt"/>；插件或测试提示应保留并释放此句柄，避免静态集合长期持有提示实例。
    /// </remarks>
    public static IDisposable RegisterPrompt(IExceptionPrompt prompt)
    {
        if (prompt == null)
            throw new ArgumentNullException(nameof(prompt));

        lock (_syncRoot)
        {
            if (_prompts.Contains(prompt))
                return EmptyPromptRegistration.Instance;
            _prompts.Add(prompt);
            return new PromptRegistration(prompt);
        }
    }

    /// <summary>
    /// 移除异常提示。
    /// </summary>
    /// <param name="prompt">异常提示。</param>
    /// <returns>成功移除时返回 <see langword="true"/>。</returns>
    public static bool RemovePrompt(IExceptionPrompt prompt)
    {
        if (prompt == null)
            return false;
        lock (_syncRoot)
            return _prompts.Remove(prompt);
    }

    /// <summary>
    /// 获取异常提示
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="isProduction">是否生产环境</param>
    /// <returns>根据异常提示组件和运行环境生成的提示文本；异常为空时返回 <see langword="null"/>。</returns>
    public static string GetPrompt(Exception exception, bool isProduction)
    {
        if (exception == null)
            return null;
        exception = exception.GetRawException();
        var prompt = GetExceptionPrompt(exception);
        if (string.IsNullOrWhiteSpace(prompt) == false)
            return prompt;
        if (exception is Warning warning)
            return warning.GetMessage(isProduction);
        return isProduction ? R.SystemError : exception.Message;
    }

    /// <summary>
    /// 获取异常提示
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>第一个非空异常提示；没有匹配提示时返回空字符串。</returns>
    private static string GetExceptionPrompt(Exception exception)
    {
        foreach (var prompt in GetPromptsSnapshot())
        {
            var result = prompt.GetPrompt(exception);
            if (result.IsEmpty() == false)
                return result;
        }
        return string.Empty;
    }

    /// <summary>
    /// 获取原始异常
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>处理后的原始异常；异常为空时返回 <see langword="null"/>。</returns>
    public static Exception GetException(Exception exception)
    {
        if (exception is null)
            return null;
        foreach (var prompt in GetPromptsSnapshot())
        {
            var result = prompt.GetRawException(exception);
            if (result != null && ReferenceEquals(result, exception) == false)
                return result;
        }
        return exception;
    }

    /// <summary>
    /// 获取异常提示组件的稳定快照。
    /// </summary>
    /// <returns>当前异常提示组件快照。</returns>
    private static IExceptionPrompt[] GetPromptsSnapshot()
    {
        lock (_syncRoot)
            return _prompts.ToArray();
    }

    /// <summary>
    /// 管理一次异常提示注册的生命周期。
    /// </summary>
    private sealed class PromptRegistration : IDisposable
    {
        /// <summary>
        /// 此注册句柄对应的异常提示组件。
        /// </summary>
        private IExceptionPrompt _prompt;

        /// <summary>
        /// 初始化一个 <see cref="PromptRegistration" /> 类型的实例。
        /// </summary>
        /// <param name="prompt">异常提示。</param>
        public PromptRegistration(IExceptionPrompt prompt)
        {
            _prompt = prompt;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var prompt = Interlocked.Exchange(ref _prompt, null);
            if (prompt != null)
                RemovePrompt(prompt);
        }
    }

    /// <summary>
    /// 表示未新增异常提示的空注册句柄。
    /// </summary>
    private sealed class EmptyPromptRegistration : IDisposable
    {
        /// <summary>
        /// 获取共享空注册句柄。
        /// </summary>
        public static readonly EmptyPromptRegistration Instance = new EmptyPromptRegistration();

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
