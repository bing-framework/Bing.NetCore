using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Gitee
{
    /// <summary>
    /// Gitee 授权提供程序
    /// </summary>
    public interface IGiteeAuthorizationProvider: IAuthorizationUrlProvider<GiteeAuthorizationRequest>
    {
    }
}
