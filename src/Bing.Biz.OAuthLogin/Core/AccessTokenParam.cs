using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bing.Exceptions;
using Bing.Validations;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 访问令牌参数
    /// </summary>
    public class AccessTokenParam : IValidation
    {
        /// <summary>
        /// 授权类型
        /// </summary>
        [Required(ErrorMessage = "授权类型[GrantType]不能为空")]
        public string GrantType { get; set; } = OAuthConst.AuthorizationCode;

        /// <summary>
        /// 授权码。授权码一旦被使用需要重新获取
        /// </summary>
        [Required(ErrorMessage = "授权码[Code]不能为空")]
        public string Code { get; set; }

        /// <summary>
        /// 重定向地址
        /// </summary>
        [Required(ErrorMessage = "重定向地址[RedirectUri]不能为空")]
        public string RedirectUri { get; set; }

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
