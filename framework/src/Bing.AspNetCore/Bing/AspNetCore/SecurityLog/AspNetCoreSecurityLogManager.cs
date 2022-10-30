using System;
using System.Threading.Tasks;
using Bing.AspNetCore.WebClientInfo;
using Bing.Clients;
using Bing.DependencyInjection;
using Bing.SecurityLog;
using Bing.Tracing;
using Bing.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.SecurityLog;

/// <summary>
/// AspNetCore安全日志管理
/// </summary>
[Dependency(ServiceLifetime.Transient, ReplaceExisting = true)]
public class AspNetCoreSecurityLogManager : DefaultSecurityLogManager
{
    /// <summary>
    /// 日志
    /// </summary>
    protected ILogger<AspNetCoreSecurityLogManager> Logger { get; }

    /// <summary>
    /// 当前用户
    /// </summary>
    protected ICurrentUser CurrentUser { get; }

    /// <summary>
    /// 当前客户端
    /// </summary>
    protected ICurrentClient CurrentClient { get; }

    /// <summary>
    /// Http上下文访问器
    /// </summary>
    protected IHttpContextAccessor HttpContextAccessor { get; }

    /// <summary>
    /// 跟踪标识提供程序
    /// </summary>
    protected ICorrelationIdProvider CorrelationIdProvider { get; }

    /// <summary>
    /// Web客户端信息提供程序
    /// </summary>
    protected IWebClientInfoProvider WebClientInfoProvider { get; }

    /// <summary>
    /// /初始化一个<see cref="DefaultSecurityLogManager"/>类型的实例
    /// </summary>
    /// <param name="securityLogOptions">安全日志选项配置</param>
    /// <param name="securityLogStore">安全日志存储器</param>
    /// <param name="logger">日志</param>
    /// <param name="currentUser">当前用户</param>
    /// <param name="currentClient">当前客户端</param>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    /// <param name="correlationIdProvider">跟踪标识提供程序</param>
    /// <param name="webClientInfoProvider">Web客户端信息提供程序</param>
    public AspNetCoreSecurityLogManager(IOptions<BingSecurityLogOptions> securityLogOptions
        , ISecurityLogStore securityLogStore
        , ILogger<AspNetCoreSecurityLogManager> logger
        , ICurrentUser currentUser
        , ICurrentClient currentClient
        , IHttpContextAccessor httpContextAccessor
        , ICorrelationIdProvider correlationIdProvider
        , IWebClientInfoProvider webClientInfoProvider) 
        : base(securityLogOptions, securityLogStore)
    {
        Logger = logger;
        CurrentUser = currentUser;
        CurrentClient = currentClient;
        HttpContextAccessor = httpContextAccessor;
        CorrelationIdProvider = correlationIdProvider;
        WebClientInfoProvider = webClientInfoProvider;
    }

    /// <summary>
    /// 创建
    /// </summary>
    protected override async Task<SecurityLogInfo> CreateAsync()
    {
        var securityLogInfo = await base.CreateAsync();
        securityLogInfo.CreationTime = DateTime.Now;

        securityLogInfo.UserId = CurrentUser.UserId;
        securityLogInfo.UserName = CurrentUser.UserName;

        securityLogInfo.ClientId = CurrentClient.Id;

        securityLogInfo.CorrelationId = CorrelationIdProvider.Get();

        securityLogInfo.ClientIpAddress = WebClientInfoProvider.ClientIpAddress;
        securityLogInfo.BrowserInfo = WebClientInfoProvider.BrowserInfo;

        return securityLogInfo;
    }
}