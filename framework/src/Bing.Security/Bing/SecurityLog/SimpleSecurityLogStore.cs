using Bing.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.SecurityLog;

/// <summary>
/// 简单安全日志存储器
/// </summary>
public class SimpleSecurityLogStore : ISecurityLogStore, ITransientDependency
{
    /// <summary>
    /// 初始化一个<see cref="SimpleSecurityLogStore"/>类型的实例
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="securityLogOptions">安全日志选项配置</param>
    public SimpleSecurityLogStore(ILogger<SimpleSecurityLogStore> logger, IOptions<BingSecurityLogOptions> securityLogOptions)
    {
        Logger = logger;
        SecurityLogOptions = securityLogOptions.Value;
    }

    /// <summary>
    /// 日志
    /// </summary>
    public ILogger<SimpleSecurityLogStore> Logger { get; set; }

    /// <summary>
    /// 安全日志选项配置
    /// </summary>
    public BingSecurityLogOptions SecurityLogOptions { get; }

    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="securityLogInfo">安全日志信息</param>
    public Task SaveAsync(SecurityLogInfo securityLogInfo)
    {
        if (!SecurityLogOptions.IsEnabled)
            return Task.CompletedTask;
        Logger.LogInformation(securityLogInfo.ToString());
        return Task.CompletedTask;
    }
}
