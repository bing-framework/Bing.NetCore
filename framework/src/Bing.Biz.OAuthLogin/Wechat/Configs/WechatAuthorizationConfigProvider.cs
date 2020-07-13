using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Wechat.Configs
{
    /// <summary>
    /// 微信授权配置提供程序
    /// </summary>
    public class WechatAuthorizationConfigProvider : AuthorizationConfigProviderBase<WechatAuthorizationConfig>, IWechatAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="WechatAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">微信授权配置</param>
        public WechatAuthorizationConfigProvider(WechatAuthorizationConfig config) : base(config)
        {
        }
    }
}
