using Bing.Exceptions;
using Bing.Validation;

namespace Bing.Domain.Services;

/// <summary>
/// 提供领域服务参数对象的通用验证能力。
/// </summary>
public abstract class ParameterBase : IVerifyModel
{
    /// <summary>
    /// 验证当前参数对象；验证失败时抛出包含首个错误信息的警告异常。
    /// </summary>
    /// <returns>验证成功时返回共享的成功验证结果。</returns>
    public virtual IValidationResult Validate()
    {
        var result = DataAnnotationValidation.Validate(this);
        if (result.IsValid)
            return ValidationResultCollection.Success;
        throw new Warning(result.First().ErrorMessage);
    }
}