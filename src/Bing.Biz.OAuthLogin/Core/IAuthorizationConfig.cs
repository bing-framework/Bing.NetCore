using Bing.Validations;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权配置
    /// </summary>
    public interface IAuthorizationConfig
    {
        /// <summary>
        /// 应用标识
        /// </summary>
        string AppId { get; set; }

        /// <summary>
        /// 应用密钥
        /// </summary>
        string AppKey { get; set; }

        /// <summary>
        /// 回调地址
        /// </summary>
        string CallbackUrl { get; set; }

        /// <summary>
        /// 授权地址
        /// </summary>
        string AuthorizationUrl { get; set; }

        /// <summary>
        /// 获取访问令牌地址
        /// </summary>
        string AccessTokenUrl { get; set; }

        /// <summary>
        /// 验证
        /// </summary>
        void Validate();
    }
}
