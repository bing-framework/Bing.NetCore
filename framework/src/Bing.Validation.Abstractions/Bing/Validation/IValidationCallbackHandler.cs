namespace Bing.Validation;

/// <summary>
/// 验证回调处理器
/// </summary>
public interface IValidationCallbackHandler
{
    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="result">验证结果</param>
    void Handle(IValidationResult result);
}
