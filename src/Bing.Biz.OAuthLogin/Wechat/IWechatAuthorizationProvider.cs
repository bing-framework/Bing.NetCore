using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Wechat
{
    /// <summary>
    /// 微信授权提供程序
    /// </summary>
    public interface IWechatAuthorizationProvider : IAuthorizationUrlProvider<WechatAuthorizationRequest>, IAccessTokenProvider
    {
    }
}
