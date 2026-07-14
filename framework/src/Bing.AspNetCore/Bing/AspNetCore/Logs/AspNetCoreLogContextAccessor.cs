using Bing.AspNetCore.WebClientInfo;
using Bing.DependencyInjection;
using Bing.Logging;
using Bing.Tracing;
using Bing.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// AspNetCore日志上下文访问器
/// </summary>
[Dependency(ServiceLifetime.Scoped, ReplaceExisting = true)]
public class AspNetCoreLogContextAccessor : LogContextAccessor
{
    /// <summary>
    /// Http上下文访问器
    /// </summary>
    protected IHttpContextAccessor HttpContextAccessor { get; }

    /// <summary>
    /// Web客户端信息提供程序
    /// </summary>
    protected IWebClientInfoProvider WebClientInfoProvider { get; }

    /// <summary>
    /// 当前用户
    /// </summary>
    protected ICurrentUser CurrentUser { get; }

    /// <summary>
    /// 主机环境
    /// </summary>
    protected IHostEnvironment HostEnvironment { get; }

    /// <summary>
    /// 初始化一个<see cref="AspNetCoreLogContextAccessor"/>类型的实例
    /// </summary>
    /// <param name="webClientInfoProvider">Web客户端信息提供程序</param>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    /// <param name="currentUser">当前用户</param>
    /// <param name="correlationIdProvider">关联标识提供程序</param>
    /// <param name="hostEnvironment">主机环境</param>
    public AspNetCoreLogContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        IWebClientInfoProvider webClientInfoProvider,
        ICurrentUser currentUser,
        ICorrelationIdProvider correlationIdProvider,
        IHostEnvironment hostEnvironment) : base(correlationIdProvider)
    {
        HttpContextAccessor = httpContextAccessor;
        WebClientInfoProvider = webClientInfoProvider;
        CurrentUser = currentUser;
        HostEnvironment = hostEnvironment;
    }

    /// <summary>
    /// 创建日志上下文
    /// </summary>
    protected override LogContextSnapshot Create()
    {
        var baseContext = base.Create();
        var httpContext = HttpContextAccessor.HttpContext;
        var data = new Dictionary<string, object>
        {
            ["UserName"] = CurrentUser.GetUserName(),
            ["FullName"] = CurrentUser.GetFullName(),
            ["TenantCode"] = CurrentUser.GetTenantCode(),
            ["TenantName"] = CurrentUser.GetTenantName()
        };
        var identity = new LogIdentityContext(
            CurrentUser.UserId,
            CurrentUser.TenantId,
            httpContext?.Features.Get<ISessionFeature>()?.Session?.Id);
        var client = new LogClientContext(
            CurrentUser.GetApplicationName() ?? HostEnvironment.ApplicationName,
            HostEnvironment.EnvironmentName,
            WebClientInfoProvider.ClientIpAddress,
            baseContext.Client.Host,
            WebClientInfoProvider.BrowserInfo,
            httpContext?.Request?.GetDisplayUrl(),
            httpContext?.Request != null);
        return new LogContextSnapshot(
            baseContext.TraceId,
            identity,
            client,
            new BusinessLogContext(data: data));
    }
}
