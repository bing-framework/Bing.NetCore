using Bing.Validation;

namespace Bing.Tests.Samples;

/// <summary>
/// 验证处理器样例 - 什么也不做
/// </summary>
public class ValidationHandlerSample : IValidationCallbackHandler
{
    /// <summary>
    /// 处理验证错误
    /// </summary>
    /// <param name="results">验证结果集合</param>
    public void Handle(IValidationResult results) { }
}