using System.Linq;
using Bing.Exceptions;
using Bing.Validations;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权配置基类
    /// </summary>
    public abstract class AuthorizationConfigBase:IAuthorizationConfig
    {
        /// <summary>
        /// 验证
        /// </summary>
        public virtual void Validate()
        {
            var result = DataAnnotationValidation.Validate(this);
            if (result.IsValid == false)
            {
                throw new Warning(result.First().ErrorMessage);
            }
        }
    }
}
