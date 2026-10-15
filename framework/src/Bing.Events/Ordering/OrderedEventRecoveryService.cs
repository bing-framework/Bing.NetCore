using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Bing.Events.Ordering;

/// <summary>
/// 表示一次有序事件恢复操作的安全审计记录。
/// </summary>
public sealed class OrderedEventRecoveryAuditEntry
{
    /// <summary>
    /// 初始化一个 <see cref="OrderedEventRecoveryAuditEntry" /> 类型的实例。
    /// </summary>
    /// <param name="operation">恢复操作名称。</param>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">业务流。</param>
    /// <param name="succeeded">操作是否成功。</param>
    /// <param name="errorType">失败异常类型；成功时为空。</param>
    public OrderedEventRecoveryAuditEntry(string operation, string consumer, OrderedEventStream stream,
        bool succeeded, string errorType = null)
    {
        Operation = string.IsNullOrWhiteSpace(operation)
            ? throw new ArgumentException("恢复操作名称不能为空。", nameof(operation))
            : operation;
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        Consumer = consumer;
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Succeeded = succeeded;
        ErrorType = errorType;
    }

    /// <summary>
    /// 恢复操作名称。
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// 逻辑消费者名称。
    /// </summary>
    public string Consumer { get; }

    /// <summary>
    /// 业务流。
    /// </summary>
    public OrderedEventStream Stream { get; }

    /// <summary>
    /// 操作是否成功。
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// 失败异常类型。
    /// </summary>
    public string ErrorType { get; }
}

/// <summary>
/// 保存有序事件人工恢复操作的关键审计记录。
/// </summary>
/// <remarks>实现应使用调用方自己的授权上下文和持久化策略，不应记录消息正文或敏感参数。</remarks>
public interface IOrderedEventRecoveryAudit
{
    /// <summary>
    /// 保存恢复操作审计记录。
    /// </summary>
    /// <param name="entry">安全审计记录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RecordAsync(OrderedEventRecoveryAuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供受授权的有序事件状态查询和人工恢复入口。
/// </summary>
public interface IOrderedEventRecoveryService
{
    /// <summary>
    /// 查询指定业务流状态。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">待查询的业务流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>业务流处理状态。</returns>
    Task<OrderedEventStreamState> GetStateAsync(string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 重试指定业务流的失败前序事件。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">待恢复的业务流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RetryAsync(string consumer, OrderedEventStream stream, CancellationToken cancellationToken = default);
}

/// <summary>
/// 统一执行授权、状态查询、人工重试和关键操作审计。
/// </summary>
public sealed class OrderedEventRecoveryService : IOrderedEventRecoveryService
{
    /// <summary>
    /// 执行有序事件查询和恢复操作的协调器。
    /// </summary>
    private readonly OrderedEventCoordinator _coordinator;

    /// <summary>
    /// 执行恢复授权的授权器集合。
    /// </summary>
    private readonly IReadOnlyList<IOrderedEventRecoveryAuthorizer> _authorizers;

    /// <summary>
    /// 记录恢复操作的审计记录器集合。
    /// </summary>
    private readonly IReadOnlyList<IOrderedEventRecoveryAudit> _audits;

    /// <summary>
    /// 记录恢复审计失败的日志记录器。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 初始化一个 <see cref="OrderedEventRecoveryService" /> 类型的实例。
    /// </summary>
    /// <param name="coordinator">有序事件协调器。</param>
    /// <param name="authorizers">恢复授权器集合。</param>
    /// <param name="audits">恢复审计记录器集合。</param>
    /// <param name="logger">可选的安全诊断日志记录器。</param>
    public OrderedEventRecoveryService(OrderedEventCoordinator coordinator,
        IEnumerable<IOrderedEventRecoveryAuthorizer> authorizers = null,
        IEnumerable<IOrderedEventRecoveryAudit> audits = null,
        ILogger<OrderedEventRecoveryService> logger = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _authorizers = authorizers?.ToArray() ?? Array.Empty<IOrderedEventRecoveryAuthorizer>();
        _audits = audits?.ToArray() ?? Array.Empty<IOrderedEventRecoveryAudit>();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrderedEventStreamState> GetStateAsync(string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken = default)
    {
        Validate(consumer, stream);
        try
        {
            await AuthorizeAsync(consumer, stream, cancellationToken).ConfigureAwait(false);
            EnsureAuditConfigured();
            var result = await _coordinator.GetStateAsync(consumer, stream, cancellationToken).ConfigureAwait(false);
            await RecordAuditAsync(new OrderedEventRecoveryAuditEntry("Query", consumer, stream, true),
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception exception)
        {
            await RecordFailureAuditAsync("Query", consumer, stream, exception, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RetryAsync(string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken = default)
    {
        Validate(consumer, stream);
        try
        {
            await AuthorizeAsync(consumer, stream, cancellationToken).ConfigureAwait(false);
            EnsureAuditConfigured();
            await _coordinator.RetryWithoutAuthorizationAsync(consumer, stream, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await RecordFailureAuditAsync("Retry", consumer, stream, exception, cancellationToken).ConfigureAwait(false);
            throw;
        }

        try
        {
            await RecordAuditAsync(new OrderedEventRecoveryAuditEntry("Retry", consumer, stream, true),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception auditException)
        {
            // 数据库恢复已提交，审计失败不能伪装成回滚；保留已提交结果和安全诊断信息。
            auditException.SetTransactionOutcome(TransactionOutcome.Committed);
            auditException.Data["Bing.OrderedEventRecoveryAuditCommitted"] = true;
            throw;
        }
    }

    /// <summary>
    /// 执行所有恢复授权器。
    /// </summary>
    private async Task AuthorizeAsync(string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken)
    {
        if (_authorizers.Count == 0)
            throw new InvalidOperationException("有序事件恢复未配置授权器，已拒绝恢复操作。");
        foreach (var authorizer in _authorizers)
            await authorizer.AuthorizeAsync(consumer, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 确认关键恢复操作具备审计记录器。
    /// </summary>
    private void EnsureAuditConfigured()
    {
        if (_audits.Count == 0)
            throw new InvalidOperationException("有序事件恢复未配置审计记录器，已拒绝操作。");
    }

    /// <summary>
    /// 保存审计记录；每个记录器都必须完成。
    /// </summary>
    private async Task RecordAuditAsync(OrderedEventRecoveryAuditEntry entry,
        CancellationToken cancellationToken)
    {
        foreach (var audit in _audits)
            await audit.RecordAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 尝试记录失败审计，不替换原始异常。
    /// </summary>
    private async Task RecordFailureAuditAsync(string operation, string consumer, OrderedEventStream stream,
        Exception failure, CancellationToken cancellationToken)
    {
        try
        {
            await RecordAuditAsync(new OrderedEventRecoveryAuditEntry(operation, consumer, stream, false,
                failure.GetType().Name), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception auditException)
        {
            failure.Data["Bing.OrderedEventRecoveryAuditException"] = auditException;
            _logger?.LogError("有序事件恢复审计失败。Operation={Operation}, Consumer={Consumer}, StreamKey={StreamKey}, ErrorType={ErrorType}",
                operation, consumer, stream.EntityKey, auditException.GetType().Name);
        }
    }

    /// <summary>
    /// 校验恢复服务入口参数。
    /// </summary>
    private static void Validate(string consumer, OrderedEventStream stream)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
    }
}

/// <summary>
/// 注册有序事件恢复服务。
/// </summary>
public static class OrderedEventRecoveryServiceExtensions
{
    /// <summary>
    /// 注册受授权的状态查询和人工恢复入口。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>调用方仍需注册至少一个 <see cref="IOrderedEventRecoveryAuthorizer"/>，并按生产要求注册持久化审计记录器。</remarks>
    public static IServiceCollection AddBingOrderedEventRecovery(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IOrderedEventRecoveryService, OrderedEventRecoveryService>();
        return services;
    }
}
