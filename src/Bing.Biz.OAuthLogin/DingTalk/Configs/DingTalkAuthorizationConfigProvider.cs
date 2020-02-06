using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.DingTalk.Configs
{
    /// <summary>
    /// 钉钉授权配置提供程序
    /// </summary>
    public class DingTalkAuthorizationConfigProvider : AuthorizationConfigProviderBase<DingTalkAuthorizationConfig>, IDingTalkAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="DingTalkAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">钉钉授权配置</param>
        public DingTalkAuthorizationConfigProvider(DingTalkAuthorizationConfig config) : base(config)
        {
        }
    }
}
