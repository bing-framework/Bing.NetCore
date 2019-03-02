using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Github.Configs
{
    /// <summary>
    /// Github授权配置提供程序
    /// </summary>
    public class GithubAuthorizationConfigProvider: AuthorizationConfigProviderBase<GithubAuthorizationConfig>,IGithubAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="GithubAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">Github授权配置</param>
        public GithubAuthorizationConfigProvider(GithubAuthorizationConfig config) : base(config)
        {
        }
    }
}
