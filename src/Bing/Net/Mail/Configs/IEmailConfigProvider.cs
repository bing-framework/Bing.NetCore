using System.Threading.Tasks;

namespace Bing.Net.Mail.Configs
{
    /// <summary>
    /// 电子邮件配置提供器
    /// </summary>
    public interface IEmailConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        EmailConfig GetConfig();

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        Task<EmailConfig> GetConfigAsync();
    }
}
