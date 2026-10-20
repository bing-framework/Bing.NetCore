using Bing.Validation;

namespace Bing.Tests.Samples;

/// <summary>
/// 验证处理器样例 - 什么也不做
/// </summary>
public class ValidationHandlerSample : IValidationCallbackHandler
{
    /// <inheritdoc />
    public void Handle(IValidationResult results) { }
}