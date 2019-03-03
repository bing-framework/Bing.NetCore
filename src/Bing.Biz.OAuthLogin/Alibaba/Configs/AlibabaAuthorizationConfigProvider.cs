using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Alibaba.Configs
{
    /// <summary>
    /// 阿里巴巴授权配置提供程序
    /// </summary>
    public class AlibabaAuthorizationConfigProvider: AuthorizationConfigProviderBase<AlibabaAuthorizationConfig>,IAlibabaAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="AlibabaAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">阿里巴巴授权配置</param>
        public AlibabaAuthorizationConfigProvider(AlibabaAuthorizationConfig config) : base(config)
        {
        }
    }
}
