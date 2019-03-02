using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.QQ.Configs
{
    /// <summary>
    /// QQ授权配置提供器
    /// </summary>
    public class QQAuthorizationConfigProvider : IQQAuthorizationConfigProvider
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly QQAuthorizationConfig _config;

        /// <summary>
        /// 初始化一个<see cref="QQAuthorizationConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">QQ授权配置</param>
        public QQAuthorizationConfigProvider(QQAuthorizationConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<QQAuthorizationConfig> GetConfigAsync()
        {
            return Task.FromResult(_config);
        }
    }
}
