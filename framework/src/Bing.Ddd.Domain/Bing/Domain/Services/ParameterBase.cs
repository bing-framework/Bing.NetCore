using System.Linq;
using Bing.Exceptions;
using Bing.Validation;

namespace Bing.Domain.Services
{
    /// <summary>
    /// 参数基类
    /// </summary>
    public abstract class ParameterBase : IVerifyModel
    {
        /// <summary>
        /// 验证
        /// </summary>
        public virtual IValidationResult Validate()
        {
            var result = DataAnnotationValidation.Validate(this);
            if (result.IsValid)
                return ValidationResultCollection.Success;
            throw new Warning(result.First().ErrorMessage);
        }
    }
}
