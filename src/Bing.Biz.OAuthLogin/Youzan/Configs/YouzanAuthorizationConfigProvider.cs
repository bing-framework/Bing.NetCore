using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Youzan.Configs
{
    /// <summary>
    /// 有赞授权配置提供程序
    /// </summary>
    public class YouzanAuthorizationConfigProvider: AuthorizationConfigProviderBase<YouzanAuthorizationConfig>,IYouzanAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="YouzanAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">有赞授权配置</param>
        public YouzanAuthorizationConfigProvider(YouzanAuthorizationConfig config) : base(config)
        {
        }
    }
}
