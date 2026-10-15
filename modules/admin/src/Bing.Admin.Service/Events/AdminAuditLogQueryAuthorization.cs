using System;
using System.Threading;
using System.Threading.Tasks;
using Bing.Auditing.Persistence;
using Bing.Users;
using Microsoft.Extensions.Configuration;

namespace Bing.Admin.Service.Events;

/// <summary>
/// 校验 Admin 审计日志元数据查询权限。
/// </summary>
/// <remarks>
/// 查询入口默认关闭；启用后需要独立角色。除非显式允许跨租户，否则查询租户必须与当前用户一致。
/// </remarks>
public sealed class AdminAuditLogQueryAuthorizer : IAuditLogQueryAuthorizer
{
    /// <summary>
    /// 当前认证用户。
    /// </summary>
    private readonly ICurrentUser _currentUser;
    /// <summary>
    /// 同租户查询所需角色。
    /// </summary>
    private readonly string _requiredRole;
    /// <summary>
    /// 是否允许访问其他租户。
    /// </summary>
    private readonly bool _allowCrossTenant;
    /// <summary>
    /// 跨租户查询所需角色。
    /// </summary>
    private readonly string _crossTenantRole;

    /// <summary>
    /// 初始化一个 <see cref="AdminAuditLogQueryAuthorizer" /> 类型的实例。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <param name="currentUser">当前认证用户。</param>
    public AdminAuditLogQueryAuthorizer(IConfiguration configuration, ICurrentUser currentUser)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        var section = configuration.GetSection("Auditing:Query");
        _requiredRole = section["Role"];
        if (string.IsNullOrWhiteSpace(_requiredRole))
            throw new InvalidOperationException("Auditing:Query:Role 必须配置为审计查询角色。");
        _allowCrossTenant = section.GetValue("AllowCrossTenant", false);
        _crossTenantRole = section["CrossTenantRole"];
        if (_allowCrossTenant && string.IsNullOrWhiteSpace(_crossTenantRole))
            throw new InvalidOperationException(
                "启用 Auditing:Query:AllowCrossTenant 时必须配置 Auditing:Query:CrossTenantRole。");
    }

    /// <inheritdoc />
    public Task AuthorizeAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        cancellationToken.ThrowIfCancellationRequested();
        if (!_currentUser.IsAuthenticated || !_currentUser.IsInRole(_requiredRole))
            throw new UnauthorizedAccessException("当前用户没有审计日志查询权限。");
        if (string.IsNullOrWhiteSpace(query.TenantId))
            throw new UnauthorizedAccessException("审计查询必须指定租户，当前入口不支持全局查询。");
        if (string.Equals(_currentUser.TenantId, query.TenantId, StringComparison.Ordinal))
            return Task.CompletedTask;
        if (!_allowCrossTenant || !_currentUser.IsInRole(_crossTenantRole))
            throw new UnauthorizedAccessException("当前用户不能查询其他租户的审计日志。");
        return Task.CompletedTask;
    }
}
