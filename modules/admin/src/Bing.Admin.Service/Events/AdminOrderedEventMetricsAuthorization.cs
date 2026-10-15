using System;
using System.Threading;
using System.Threading.Tasks;
using Bing.Users;
using Microsoft.Extensions.Configuration;

namespace Bing.Admin.Service.Events;

/// <summary>
/// 校验 Admin 有序事件指标的读取权限。
/// </summary>
/// <remarks>指标端点默认关闭；启用后必须配置独立的角色，避免把运行诊断数据暴露给普通用户。</remarks>
public sealed class AdminOrderedEventMetricsAuthorizer
{
    /// <summary>
    /// 提供当前用户身份和角色信息的访问器。
    /// </summary>
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 读取有序事件指标所需的角色。
    /// </summary>
    private readonly string _requiredRole;

    /// <summary>
    /// 初始化一个 <see cref="AdminOrderedEventMetricsAuthorizer" /> 类型的实例。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <param name="currentUser">当前认证用户。</param>
    public AdminOrderedEventMetricsAuthorizer(IConfiguration configuration, ICurrentUser currentUser)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _requiredRole = configuration["OrderedEvents:Metrics:Role"];
        if (string.IsNullOrWhiteSpace(_requiredRole))
            throw new InvalidOperationException("OrderedEvents:Metrics:Role 必须配置为指标读取角色。");
    }

    /// <summary>
    /// 校验当前用户是否可以读取有序事件指标。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示授权检查完成的异步操作。</returns>
    public Task AuthorizeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_currentUser.IsAuthenticated || !_currentUser.IsInRole(_requiredRole))
            throw new UnauthorizedAccessException("当前用户没有有序事件指标读取权限。");
        return Task.CompletedTask;
    }
}
