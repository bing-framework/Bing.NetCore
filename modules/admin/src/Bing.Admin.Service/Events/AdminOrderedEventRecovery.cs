using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Auditing;
using Bing.Data.Events.Ordering;
using Bing.Events.Ordering;
using Bing.Users;
using Microsoft.Extensions.Configuration;

namespace Bing.Admin.Service.Events;

/// <summary>
/// 校验 Admin 有序事件人工恢复权限。
/// </summary>
/// <remarks>
/// 恢复功能默认关闭；启用时必须同时配置角色和消费者白名单。租户事件只能由同租户用户恢复，
/// 不使用请求中的消息头或查询参数替代当前用户身份。
/// </remarks>
public sealed class AdminOrderedEventRecoveryAuthorizer : IOrderedEventRecoveryAuthorizer
{
    /// <summary>
    /// 提供当前用户身份和角色信息的访问器。
    /// </summary>
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 执行人工恢复所需的角色。
    /// </summary>
    private readonly string _requiredRole;

    /// <summary>
    /// 允许当前用户恢复的逻辑消费者集合。
    /// </summary>
    private readonly HashSet<string> _consumers;

    /// <summary>
    /// 指示是否要求请求租户与事件租户一致。
    /// </summary>
    private readonly bool _requireTenantMatch;

    /// <summary>
    /// 初始化一个 <see cref="AdminOrderedEventRecoveryAuthorizer" /> 类型的实例。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <param name="currentUser">当前认证用户。</param>
    public AdminOrderedEventRecoveryAuthorizer(IConfiguration configuration, ICurrentUser currentUser)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        var section = configuration.GetSection("OrderedEvents:Recovery");
        _requiredRole = section["Role"];
        if (string.IsNullOrWhiteSpace(_requiredRole))
            throw new InvalidOperationException("OrderedEvents:Recovery:Role 必须配置为人工恢复角色。");
        var consumers = section.GetSection("Consumers").GetChildren()
            .Select(item => item.Value ?? item.Key)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        if (consumers.Length == 0)
            throw new InvalidOperationException("OrderedEvents:Recovery:Consumers 必须至少配置一个逻辑消费者。");
        foreach (var consumer in consumers)
        {
            if (consumer.Length > 200)
                throw new InvalidOperationException("OrderedEvents:Recovery:Consumers 中的逻辑消费者不能超过 200 个字符。");
            if (!string.Equals(consumer, consumer.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("OrderedEvents:Recovery:Consumers 中的逻辑消费者不能包含首尾空白。");
        }
        // 有序事件存储按 Ordinal 区分消费者大小写，授权白名单必须使用相同身份语义。
        _consumers = new HashSet<string>(consumers, StringComparer.Ordinal);
        _requireTenantMatch = !bool.TryParse(section["RequireTenantMatch"], out var value) || value;
    }

    /// <inheritdoc />
    public Task AuthorizeAsync(string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(consumer) || stream == null)
            throw new ArgumentException("恢复请求缺少消费者或业务流。", nameof(stream));
        if (!_currentUser.IsAuthenticated || !_currentUser.IsInRole(_requiredRole))
            throw new UnauthorizedAccessException("当前用户没有有序事件恢复权限。");
        if (!_consumers.Contains(consumer))
            throw new UnauthorizedAccessException("当前用户没有该逻辑消费者的恢复权限。");
        if (_requireTenantMatch && !string.IsNullOrEmpty(stream.TenantId) &&
            !string.Equals(_currentUser.TenantId, stream.TenantId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("当前用户不能恢复其他租户的有序事件。");
        return Task.CompletedTask;
    }
}

/// <summary>
/// 将有序事件恢复结果写入当前审计存储。
/// </summary>
/// <remarks>只写入操作、消费者和流标识，不写入事件正文；审计发生在恢复事务之后。</remarks>
public sealed class AdminOrderedEventRecoveryAudit : IOrderedEventRecoveryAudit
{
    /// <summary>
    /// 创建并保存审计记录的管理器。
    /// </summary>
    private readonly IAuditingManager _auditingManager;

    /// <summary>
    /// 提供当前用户身份和租户信息的访问器。
    /// </summary>
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 写入审计日志的应用名称。
    /// </summary>
    private readonly string _applicationName;

    /// <summary>
    /// 初始化一个 <see cref="AdminOrderedEventRecoveryAudit" /> 类型的实例。
    /// </summary>
    /// <param name="auditingManager">审计管理器。</param>
    /// <param name="currentUser">当前认证用户。</param>
    /// <param name="configuration">宿主配置。</param>
    public AdminOrderedEventRecoveryAudit(IAuditingManager auditingManager, ICurrentUser currentUser,
        IConfiguration configuration)
    {
        _auditingManager = auditingManager ?? throw new ArgumentNullException(nameof(auditingManager));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _applicationName = configuration?.GetValue<string>("ApplicationName") ?? "Bing.Admin";
    }

    /// <inheritdoc />
    public async Task RecordAsync(OrderedEventRecoveryAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        cancellationToken.ThrowIfCancellationRequested();
        var handle = _auditingManager.BeginAsyncScope();
        try
        {
            var log = _auditingManager.Current?.Log ??
                throw new InvalidOperationException("审计作用域创建失败。");
            log.ApplicationName = _applicationName;
            log.UserId = _currentUser.UserId;
            log.UserName = _currentUser.UserName;
            log.TenantId = string.IsNullOrEmpty(entry.Stream.TenantId)
                ? _currentUser.TenantId
                : entry.Stream.TenantId;
            log.Actions.Add(new AuditLogActionInfo
            {
                ServiceName = "OrderedEventRecovery",
                MethodName = entry.Operation,
                ExecutionTime = DateTime.UtcNow,
                ExecutionDuration = 0
            });
            log.Comments.Add("Consumer=" + entry.Consumer);
            log.Comments.Add("Stream=" + entry.Stream.Stream);
            log.Comments.Add("EntityKey=" + entry.Stream.EntityKey);
            log.Comments.Add("Succeeded=" + entry.Succeeded);
            if (!string.IsNullOrWhiteSpace(entry.ErrorType))
                log.Comments.Add("ErrorType=" + entry.ErrorType);
            await handle.SaveAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await handle.DisposeAsync().ConfigureAwait(false);
        }
    }
}
