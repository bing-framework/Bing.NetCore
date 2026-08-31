namespace Bing.Emailing;

/// <summary>
/// 提供固定电子邮件配置的默认配置提供程序。
/// </summary>
public class DefaultEmailConfigProvider : IEmailConfigProvider
{
    /// <summary>
    /// 保存由该提供程序返回的电子邮件配置。
    /// </summary>
    private readonly EmailConfig _config;

    /// <summary>
    /// 使用指定电子邮件配置初始化一个 <see cref="DefaultEmailConfigProvider"/> 实例。
    /// </summary>
    /// <param name="config">电子邮件配置。</param>
    public DefaultEmailConfigProvider(EmailConfig config) => _config = config;

    /// <summary>
    /// 获取当前电子邮件配置。
    /// </summary>
    /// <returns>当前电子邮件配置。</returns>
    public EmailConfig GetConfig() => _config;

    /// <summary>
    /// 异步获取当前电子邮件配置。
    /// </summary>
    /// <returns>表示异步获取操作的任务，结果为当前电子邮件配置。</returns>
    public Task<EmailConfig> GetConfigAsync() => Task.FromResult(_config);
}