namespace Bing.MailKit.Configs;

/// <summary>
/// MailKit 配置提供器
/// </summary>
public interface IMailKitConfigProvider
{
    /// <summary>
    /// 获取配置
    /// </summary>
    /// <returns>当前 MailKit 配置。</returns>
    MailKitConfig GetConfig();

    /// <summary>
    /// 获取配置
    /// </summary>
    /// <returns>表示异步获取操作的任务，结果为当前 MailKit 配置。</returns>
    Task<MailKitConfig> GetConfigAsync();
}
