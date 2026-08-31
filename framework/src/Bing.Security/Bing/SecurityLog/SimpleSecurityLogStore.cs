using Bing.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.SecurityLog;

/// <summary>
/// 将安全日志输出到应用程序日志的默认存储器。
/// </summary>
public class SimpleSecurityLogStore : ISecurityLogStore, ITransientDependency
{
    /// <summary>
    /// 使用日志记录器和安全日志选项初始化 <see cref="SimpleSecurityLogStore"/> 的实例。
    /// </summary>
    /// <param name="logger">用于输出安全日志的日志记录器。</param>
    /// <param name="securityLogOptions">提供安全日志启用状态的选项。</param>
    public SimpleSecurityLogStore(ILogger<SimpleSecurityLogStore> logger, IOptions<BingSecurityLogOptions> securityLogOptions)
    {
        Logger = logger;
        SecurityLogOptions = securityLogOptions.Value;
    }

    /// <summary>
    /// 获取或设置用于输出安全日志的日志记录器。
    /// </summary>
    public ILogger<SimpleSecurityLogStore> Logger { get; set; }

    /// <summary>
    /// 获取安全日志选项配置。
    /// </summary>
    public BingSecurityLogOptions SecurityLogOptions { get; }

    /// <inheritdoc />
    /// <remarks>安全日志启用时仅以信息级别输出日志文本；禁用时不执行任何操作。</remarks>
    public Task SaveAsync(SecurityLogInfo securityLogInfo)
    {
        if (!SecurityLogOptions.IsEnabled)
            return Task.CompletedTask;
        Logger.LogInformation(securityLogInfo.ToString());
        return Task.CompletedTask;
    }
}
