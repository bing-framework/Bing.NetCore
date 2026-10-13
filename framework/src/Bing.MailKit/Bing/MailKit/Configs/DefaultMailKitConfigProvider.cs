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
    /// 初始化一个 <see cref="DefaultMailKitConfigProvider"/> 类型的实例。
    /// </summary>
    /// <param name="config">MailKit 配置。</param>
    public DefaultMailKitConfigProvider(MailKitConfig config) => _config = config;

    /// <inheritdoc />
    public MailKitConfig GetConfig() => _config;

    /// <inheritdoc />
    /// <remarks>
    /// 异步获取当前 MailKit 配置。
    /// </remarks>
    public Task<MailKitConfig> GetConfigAsync() => Task.FromResult(_config);
}
