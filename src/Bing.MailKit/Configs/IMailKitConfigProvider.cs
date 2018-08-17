using System.Threading.Tasks;

namespace Bing.MailKit.Configs
{
    /// <summary>
    /// MailKit 配置提供器
    /// </summary>
    public interface IMailKitConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        MailKitConfig GetConfig();

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        Task<MailKitConfig> GetConfigAsync();
    }
}
