﻿using System.Threading.Tasks;

namespace Bing.MailKit.Configs;

/// <summary>
/// MailKit默认配置提供器
/// </summary>
public class DefaultMailKitConfigProvider : IMailKitConfigProvider
{
    /// <summary>
    /// 配置
    /// </summary>
    private readonly MailKitConfig _config;

    /// <summary>
    /// 初始化一个<see cref="DefaultMailKitConfigProvider"/>类型的实例
    /// </summary>
    /// <param name="config">MailKit 配置</param>
    public DefaultMailKitConfigProvider(MailKitConfig config) => _config = config;

    /// <summary>
    /// 获取配置
    /// </summary>
    public MailKitConfig GetConfig() => _config;

    /// <summary>
    /// 获取配置
    /// </summary>
    public Task<MailKitConfig> GetConfigAsync() => Task.FromResult(_config);
}