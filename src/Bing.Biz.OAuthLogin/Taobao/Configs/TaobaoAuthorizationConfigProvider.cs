using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Taobao.Configs
{
    /// <summary>
    /// 淘宝授权配置提供程序
    /// </summary>
    public class TaobaoAuthorizationConfigProvider:AuthorizationConfigProviderBase<TaobaoAuthorizationConfig>,ITaobaoAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="TaobaoAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">淘宝授权配置</param>
        public TaobaoAuthorizationConfigProvider(TaobaoAuthorizationConfig config) : base(config)
        {
        }
    }
}
