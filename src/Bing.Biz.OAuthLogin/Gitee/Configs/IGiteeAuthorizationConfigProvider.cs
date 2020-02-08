using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Gitee.Configs
{
    /// <summary>
    /// Gitee 授权配置提供程序
    /// </summary>
    public interface IGiteeAuthorizationConfigProvider : IAuthorizationConfigProvider<GiteeAuthorizationConfig>
    {
    }
}
