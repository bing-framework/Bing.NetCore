using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Coding.Configs
{
    /// <summary>
    /// Coding.NET 授权配置提供程序
    /// </summary>
    public class CodingAuthorizationConfigProvider: AuthorizationConfigProviderBase<CodingAuthorizationConfig>,ICodingAuthorizationConfigProvider
    {
        /// <summary>
        /// 初始化一个<see cref="CodingAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">Coding.NET 授权配置</param>
        public CodingAuthorizationConfigProvider(CodingAuthorizationConfig config) : base(config)
        {
        }
    }
}
