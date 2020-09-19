using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.DingTalk
{
    /// <summary>
    /// 钉钉授权提供程序
    /// </summary>
    public interface IDingTalkAuthorizationProvider : IAuthorizationUrlProvider<DingTalkAuthorizationRequest>
    {
    }
}
