using System;
using System.Threading.Tasks;
using Bing.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.SecurityLog;

/// <summary>
/// 默认安全日志管理
/// </summary>
public class DefaultSecurityLogManager : ISecurityLogManager, ITransientDependency
{
    /// <summary>
    /// 安全日志选项配置
    /// </summary>
    protected BingSecurityLogOptions SecurityLogOptions { get; }

    /// <summary>
    /// 安全日志存储器
    /// </summary>
    protected ISecurityLogStore SecurityLogStore { get; }

    /// <summary>
    /// /初始化一个<see cref="DefaultSecurityLogManager"/>类型的实例
    /// </summary>
    /// <param name="securityLogOptions">安全日志选项配置</param>
    /// <param name="securityLogStore">安全日志存储器</param>
    public DefaultSecurityLogManager(IOptions<BingSecurityLogOptions> securityLogOptions, ISecurityLogStore securityLogStore)
    {
        SecurityLogOptions = securityLogOptions.Value;
        SecurityLogStore = securityLogStore;
    }

    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="saveAction">保存操作</param>
    public async Task SaveAsync(Action<SecurityLogInfo> saveAction = null)
    {
        if (!SecurityLogOptions.IsEnabled)
            return;
        var securityLogInfo = await CreateAsync();
        saveAction?.Invoke(securityLogInfo);
        await SecurityLogStore.SaveAsync(securityLogInfo);
    }

    /// <summary>
    /// 创建
    /// </summary>
    protected virtual Task<SecurityLogInfo> CreateAsync() => Task.FromResult(new SecurityLogInfo { ApplicationName = SecurityLogOptions.ApplicationName });
}