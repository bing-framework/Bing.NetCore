using Bing.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.SecurityLog;

/// <summary>
/// 使用配置和安全日志存储器创建、补充并保存安全日志的默认管理器。
/// </summary>
public class DefaultSecurityLogManager : ISecurityLogManager, ITransientDependency
{
    /// <summary>
    /// 使用安全日志配置和存储器初始化 <see cref="DefaultSecurityLogManager"/> 的实例。
    /// </summary>
    /// <param name="securityLogOptions">提供安全日志启用状态和应用程序名称的选项。</param>
    /// <param name="securityLogStore">保存已创建安全日志的存储器。</param>
    public DefaultSecurityLogManager(IOptions<BingSecurityLogOptions> securityLogOptions, ISecurityLogStore securityLogStore)
    {
        SecurityLogOptions = securityLogOptions.Value;
        SecurityLogStore = securityLogStore;
    }

    /// <summary>
    /// 获取安全日志选项配置。
    /// </summary>
    protected BingSecurityLogOptions SecurityLogOptions { get; }

    /// <summary>
    /// 获取安全日志存储器。
    /// </summary>
    protected ISecurityLogStore SecurityLogStore { get; }

    /// <inheritdoc />
    /// <remarks>安全日志功能未启用时不会创建日志、调用补充操作或写入存储器。</remarks>
    public async Task SaveAsync(Action<SecurityLogInfo> saveAction = null)
    {
        if (!SecurityLogOptions.IsEnabled)
            return;
        var securityLogInfo = await CreateAsync();
        saveAction?.Invoke(securityLogInfo);
        await SecurityLogStore.SaveAsync(securityLogInfo);
    }

    /// <summary>
    /// 创建包含当前应用程序名称的初始安全日志。
    /// </summary>
    /// <returns>表示异步操作的任务，结果为包含当前应用程序名称的安全日志。</returns>
    protected virtual Task<SecurityLogInfo> CreateAsync() => Task.FromResult(new SecurityLogInfo { ApplicationName = SecurityLogOptions.ApplicationName });
}
