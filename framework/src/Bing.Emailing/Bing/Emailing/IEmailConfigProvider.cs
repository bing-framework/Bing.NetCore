namespace Bing.Emailing;

/// <summary>
/// 电子邮件配置提供器
/// </summary>
public interface IEmailConfigProvider
{
    /// <summary>
    /// 获取配置
    /// </summary>
    /// <returns>当前电子邮件配置。</returns>
    EmailConfig GetConfig();

    /// <summary>
    /// 获取配置
    /// </summary>
    /// <returns>表示异步获取操作的任务，结果为当前电子邮件配置。</returns>
    Task<EmailConfig> GetConfigAsync();
}