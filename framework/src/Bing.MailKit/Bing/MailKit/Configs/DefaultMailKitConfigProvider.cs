namespace Bing.MailKit.Configs;

/// <summary>
/// 提供固定 MailKit 配置的默认配置提供程序。
/// </summary>
public class DefaultMailKitConfigProvider : IMailKitConfigProvider
{
    /// <summary>
    /// 保存由该提供程序返回的 MailKit 配置。
    /// </summary>
    private readonly MailKitConfig _config;

    /// <summary>
    /// 使用指定 MailKit 配置初始化一个 <see cref="DefaultMailKitConfigProvider"/> 实例。
    /// </summary>
    /// <param name="config">MailKit 配置。</param>
    public DefaultMailKitConfigProvider(MailKitConfig config) => _config = config;

    /// <summary>
    /// 获取当前 MailKit 配置。
    /// </summary>
    /// <returns>当前 MailKit 配置。</returns>
    public MailKitConfig GetConfig() => _config;

    /// <summary>
    /// 异步获取当前 MailKit 配置。
    /// </summary>
    /// <returns>表示异步获取操作的任务，结果为当前 MailKit 配置。</returns>
    public Task<MailKitConfig> GetConfigAsync() => Task.FromResult(_config);
}
