using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Github.Configs
{
    /// <summary>
    /// Github授权配置提供程序
    /// </summary>
    public interface IGithubAuthorizationConfigProvider: IAuthorizationConfigProvider<GithubAuthorizationConfig>
    {
    }
}
