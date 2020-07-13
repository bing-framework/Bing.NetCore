using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bing.Exceptions;
using Bing.Mapping;
using Bing.Validations;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权用户参数基类
    /// </summary>
    public class AuthorizationUserParamBase : IValidation
    {
        /// <summary>
        /// 访问令牌
        /// </summary>
        [Required(ErrorMessage = "访问令牌[AccessToken]不能为空")]
        public string AccessToken { get; set; }

        /// <summary>
        /// 验证
        /// </summary>
        public ValidationResultCollection Validate()
        {
            var result = DataAnnotationValidation.Validate(this);
            if (result.IsValid)
                return ValidationResultCollection.Success;
            throw new Warning(result.First().ErrorMessage);
        }

        /// <summary>
        /// 转换为授权参数
        /// </summary>
        public virtual AuthorizationParam ToParam() => this.MapTo<AuthorizationParam>();
    }
}
