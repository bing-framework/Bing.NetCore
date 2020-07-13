using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Youzan
{
    /// <summary>
    /// 有赞授权提供程序
    /// </summary>
    public interface IYouzanAuthorizationProvider : IAuthorizationUrlProvider<YouzanAuthorizationRequest>
    {
    }
}
