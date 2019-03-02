using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bing.Biz.OAuthLogin.Core;
using Bing.Exceptions;
using Bing.Validations;

namespace Bing.Biz.OAuthLogin.QQ.Configs
{
    /// <summary>
    /// QQ授权配置
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class QQAuthorizationConfig:IAuthorizationConfig
    {
        /// <summary>
        /// 应用标识
        /// </summary>
        [Required(ErrorMessage = "应用标识[AppId]不能为空")]
        public string AppId { get; set; }

        /// <summary>
        /// 应用密钥
        /// </summary>
        [Required(ErrorMessage = "应用密钥[AppKey]不能为空")]
        public string AppKey { get; set; }

        /// <summary>
        /// 回调地址
        /// </summary>
        [Required(ErrorMessage = "回调地址[CallbackUrl]不能为空")]
        public string CallbackUrl { get; set; }

        /// <summary>
        /// 验证
        /// </summary>
        public void Validate()
        {
            var result = DataAnnotationValidation.Validate(this);
            if (result.IsValid == false)
            {
                throw new Warning(result.First().ErrorMessage);
            }
        }
    }
}
