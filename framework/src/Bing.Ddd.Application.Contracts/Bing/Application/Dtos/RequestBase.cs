using Bing.Exceptions;
using Bing.Validation;

namespace Bing.Application.Dtos;

/// <summary>
/// 提供应用层请求对象的通用验证能力。
/// </summary>
public abstract class RequestBase : IRequest
{
    /// <inheritdoc />
    /// <remarks>
    /// 验证当前请求对象；验证失败时抛出包含首个错误信息的警告异常。
    /// </remarks>
    public virtual IValidationResult Validate()
    {
        var result = DataAnnotationValidation.Validate(this);
        if (result.IsValid)
            return ValidationResultCollection.Success;
        throw new Warning(result.First().ErrorMessage);
    }
}
