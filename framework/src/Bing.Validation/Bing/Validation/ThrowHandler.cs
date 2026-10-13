using Bing.Exceptions;

namespace Bing.Validation;

/// <summary>
/// 验证失败，抛出异常 - 默认验证处理器
/// </summary>
public class ThrowHandler : IValidationCallbackHandler
{
    /// <inheritdoc />
    public void Handle(IValidationResult result)
    {
        if (result.IsValid)
            return;
        throw new Warning(result.First().ErrorMessage);
    }
}
