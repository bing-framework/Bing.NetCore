using System.Threading.Tasks;

namespace Bing.MailKit.Configs;

/// <summary>
/// MailKit 配置提供器
/// </summary>
public interface IMailKitConfigProvider
{
    /// <summary>
    /// 获取配置
    /// </summary>
    MailKitConfig GetConfig();

    /// <summary>
    /// 获取配置
    /// </summary>
    Task<MailKitConfig> GetConfigAsync();
}