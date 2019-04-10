using System.Linq;
using Bing.Exceptions;
using Bing.Validations;

namespace Bing.Applications.Dtos
{
    /// <summary>
    /// 请求对象
    /// </summary>
    public abstract class RequestBase:IRequest
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <returns></returns>
        public virtual ValidationResultCollection Validate()
        {
            var result = DataAnnotationValidation.Validate(this);
            if (result.IsValid)
            {
                return ValidationResultCollection.Success;
            }

            throw new Warning(result.First().ErrorMessage);
        }
    }
}
