using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.OsChina.Configs
{
    /// <summary>
    /// 开源中国授权配置提供程序
    /// </summary>
    public class OsChinaAuthorizationConfigProvider: AuthorizationConfigProviderBase<OsChinaAuthorizationConfig>,IOsChinaAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="OsChinaAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">开源中国授权配置</param>
        public OsChinaAuthorizationConfigProvider(OsChinaAuthorizationConfig config) : base(config)
        {
        }
    }
}
