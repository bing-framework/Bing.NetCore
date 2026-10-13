namespace Bing.Validation;

/// <summary>
/// 验证失败，不做任何处理
/// </summary>
public class NothingHandler : IValidationCallbackHandler
{
    /// <inheritdoc />
    public void Handle(IValidationResult result)
    {
    }
}
