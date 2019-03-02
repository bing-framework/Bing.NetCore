using Bing.Validations;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权配置
    /// </summary>
    public interface IAuthorizationConfig
    {
        /// <summary>
        /// 验证
        /// </summary>
        void Validate();
    }
}
