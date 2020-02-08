using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Weibo.Configs
{
    /// <summary>
    /// 微博授权配置提供程序
    /// </summary>
    public class WeiboAuthorizationConfigProvider : AuthorizationConfigProviderBase<WeiboAuthorizationConfig>, IWeiboAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="WeiboAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">微博授权配置</param>
        public WeiboAuthorizationConfigProvider(WeiboAuthorizationConfig config) : base(config)
        {
        }
    }
}
