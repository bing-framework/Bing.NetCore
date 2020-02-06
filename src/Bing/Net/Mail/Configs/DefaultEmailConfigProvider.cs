using System.Threading.Tasks;

namespace Bing.Net.Mail.Configs
{
    /// <summary>
    /// 电子邮件默认配置提供器
    /// </summary>
    public class DefaultEmailConfigProvider : IEmailConfigProvider
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly EmailConfig _config;

        /// <summary>
        /// 初始化一个<see cref="DefaultEmailConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">电子邮件配置</param>
        public DefaultEmailConfigProvider(EmailConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public EmailConfig GetConfig()
        {
            return _config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<EmailConfig> GetConfigAsync()
        {
            return Task.FromResult(_config);
        }
    }
}
