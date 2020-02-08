using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权配置提供程序基类
    /// </summary>
    /// <typeparam name="TAuthorizationConfig">授权配置类型</typeparam>
    public abstract class AuthorizationConfigProviderBase<TAuthorizationConfig> : IAuthorizationConfigProvider<TAuthorizationConfig> where TAuthorizationConfig : IAuthorizationConfig
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly TAuthorizationConfig _config;

        /// <summary>
        /// 初始化一个<see cref="AuthorizationConfigProviderBase{TAuthorizationConfig}"/>类型的实例
        /// </summary>
        /// <param name="config">授权配置</param>
        protected AuthorizationConfigProviderBase(TAuthorizationConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public virtual Task<TAuthorizationConfig> GetConfigAsync()
        {
            return Task.FromResult(_config);
        }
    }
}
