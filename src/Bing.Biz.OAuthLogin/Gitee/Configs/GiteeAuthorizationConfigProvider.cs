using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Gitee.Configs
{
    /// <summary>
    /// Gitee 授权配置提供程序
    /// </summary>
    public class GiteeAuthorizationConfigProvider : AuthorizationConfigProviderBase<GiteeAuthorizationConfig>, IGiteeAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="GiteeAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">Gitee 授权配置</param>
        public GiteeAuthorizationConfigProvider(GiteeAuthorizationConfig config) : base(config)
        {
        }
    }
}
