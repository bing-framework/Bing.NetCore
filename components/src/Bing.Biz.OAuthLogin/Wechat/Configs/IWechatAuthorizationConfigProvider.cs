using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Wechat.Configs
{
    /// <summary>
    /// 微信授权配置提供程序
    /// </summary>
    public interface IWechatAuthorizationConfigProvider : IAuthorizationConfigProvider<WechatAuthorizationConfig>
    {
    }
}
