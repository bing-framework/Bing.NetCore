using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Jd.Configs
{
    /// <summary>
    /// 京东授权配置提供程序
    /// </summary>
    public class JdAuthorizationConfigProvider : AuthorizationConfigProviderBase<JdAuthorizationConfig>, IJdAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="JdAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">京东授权配置</param>
        public JdAuthorizationConfigProvider(JdAuthorizationConfig config) : base(config)
        {
        }
    }
}
