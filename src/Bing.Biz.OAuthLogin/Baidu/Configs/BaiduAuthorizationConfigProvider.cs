using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Baidu.Configs
{
    /// <summary>
    /// 百度授权配置提供程序
    /// </summary>
    public class BaiduAuthorizationConfigProvider: AuthorizationConfigProviderBase<BaiduAuthorizationConfig>,IBaiduAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="BaiduAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">百度授权配置</param>
        public BaiduAuthorizationConfigProvider(BaiduAuthorizationConfig config) : base(config)
        {
        }
    }
}
