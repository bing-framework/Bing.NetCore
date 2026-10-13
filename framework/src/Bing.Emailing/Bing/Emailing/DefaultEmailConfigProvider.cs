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
    /// 初始化一个 <see cref="DefaultEmailConfigProvider"/> 类型的实例。
    /// </summary>
    /// <param name="config">电子邮件配置。</param>
    public DefaultEmailConfigProvider(EmailConfig config) => _config = config;

    /// <inheritdoc />
    public EmailConfig GetConfig() => _config;

    /// <inheritdoc />
    public Task<EmailConfig> GetConfigAsync() => Task.FromResult(_config);
}