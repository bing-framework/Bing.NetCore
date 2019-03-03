using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.MeiliShuo
{
    /// <summary>
    /// 美丽说授权提供程序
    /// </summary>
    public interface IMeiliShuoAuthorizationProvider: IAuthorizationUrlProvider<MeiliShuoAuthorizationRequest>
    {
    }
}
