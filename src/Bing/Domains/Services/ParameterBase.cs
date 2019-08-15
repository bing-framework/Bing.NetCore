using System.Linq;
using Bing.Exceptions;
using Bing.Validations;

namespace Bing.Domains.Services
{
    /// <summary>
    /// 参数基类
    /// </summary>
    public abstract class ParameterBase : IValidation
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <returns></returns>
        public virtual ValidationResultCollection Validate()
        {
            var result = DataAnnotationValidation.Validate(this);
            if (result.IsValid)
                return ValidationResultCollection.Success;
            throw new Warning(result.First().ErrorMessage);
        }
    }
}
