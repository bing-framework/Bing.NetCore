namespace Bing.Validation;

/// <summary>
/// 验证失败，不做任何处理
/// </summary>
public class NothingHandler : IValidationCallbackHandler
{
    /// <summary>
    /// 处理验证错误
    /// </summary>
    /// <param name="result">验证结果</param>
    public void Handle(IValidationResult result)
    {
    }
}
