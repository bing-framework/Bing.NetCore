using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;
using Bing.Aspects;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Bing.Events.Messages;
using Bing.Events.Ordering;

namespace Bing.Admin.Data.Inventory;

/// <summary>
/// 在 Admin 工作单元中提交整单库存、关键审计和 CAP 出箱。
/// </summary>
/// <remarks>每次操作使用独立服务作用域。失败后丢弃作用域；Unknown 先用相同整单身份查询结果，不能换请求标识重做。</remarks>
[IgnoreAspect]
public sealed class AdminInventoryService
{
    /// <summary>
    /// 整单完成的稳定消息主题。
    /// </summary>
    public const string CompletedTopic = "bing.admin.inventory.completed";

    /// <summary>
    /// 有序库存完成通知主题。
    /// </summary>
    public const string CompletedOrderedTopic = "bing.admin.inventory.completed.ordered.v1";

    /// <summary>
    /// 有序库存完成通知的业务流名称。
    /// </summary>
    public const string OrderedStream = "inventory-order";

    /// <summary>
    /// 有序库存完成通知的稳定逻辑消费者名称。
    /// </summary>
    public const string OrderedConsumer = "admin-inventory-v1";

    /// <summary>
    /// 当前作用域的 Admin 工作单元。
    /// </summary>
    private readonly IAdminUnitOfWork _unitOfWork;
    /// <summary>
    /// 当前工作单元共享的事务回调管理器。
    /// </summary>
    private readonly ITransactionActionManager _actions;
    /// <summary>
    /// 仅在事务回调内取得实际业务事务的作用域访问器。
    /// </summary>
    private readonly Func<DbTransaction> _currentTransaction;
    /// <summary>
    /// 维护整单身份和首次结果的库存存储。
    /// </summary>
    private readonly IInventoryOrderStore _orders;
    /// <summary>
    /// 借用业务事务保存关键审计的存储。
    /// </summary>
    private readonly ITransactionalAuditingStore _audits;
    /// <summary>
    /// 当前作用域的 CAP 消息入口。
    /// </summary>
    private readonly IMessageEventBus _events;
    /// <summary>
    /// 可选的有序 CAP 消息入口。
    /// </summary>
    private readonly OrderedEventPublisher _orderedEvents;
    /// <summary>
    /// 当前服务实例是否已开始过操作。
    /// </summary>
    private int _started;

    /// <summary>
    /// 初始化一个 <see cref="AdminInventoryService" /> 类型的实例。
    /// </summary>
    /// <param name="unitOfWork">当前作用域工作单元。</param>
    /// <param name="actions">与工作单元共享的事务回调管理器。</param>
    /// <param name="currentTransaction">当前作用域的实际事务访问器。</param>
    /// <param name="orders">整单库存存储。</param>
    /// <param name="audits">关键审计存储。</param>
    /// <param name="events">CAP 事务消息入口。</param>
    /// <param name="orderedEvents">可选的有序 CAP 事务消息入口。</param>
    public AdminInventoryService(IAdminUnitOfWork unitOfWork, ITransactionActionManager actions,
        Func<DbTransaction> currentTransaction, IInventoryOrderStore orders,
        ITransactionalAuditingStore audits, IMessageEventBus events,
        OrderedEventPublisher orderedEvents = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _currentTransaction = currentTransaction ?? throw new ArgumentNullException(nameof(currentTransaction));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _audits = audits ?? throw new ArgumentNullException(nameof(audits));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _orderedEvents = orderedEvents;
    }

    /// <summary>
    /// 提交整单库存请求。
    /// </summary>
    /// <param name="request">来自已授权业务调用的租户和整单请求。</param>
    /// <param name="userId">可信调用上下文的用户标识。</param>
    /// <param name="traceId">调用关联标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已提交的首次结果；重复请求不再次写审计或出箱。</returns>
    public async Task<InventoryOrderResult> ExecuteAsync(InventoryOrderRequest request, string userId = null,
        string traceId = null, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        if (_actions.Count != 0)
            throw new InvalidOperationException("库存业务必须使用没有待提交事务回调的独立工作单元作用域。");
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("库存业务作用域已经使用，请创建新的作用域。");
        InventoryOrderResult result = null;
        _actions.Register(async _ =>
        {
            var transaction = _currentTransaction() ??
                throw new InvalidOperationException("Admin 工作单元未建立活动库存事务。");
            result = await _orders.ExecuteAsync(transaction, request, async (current, completed, token) =>
            {
                var audit = new AuditLogInfo
                {
                    ApplicationName = "Bing.Admin.Inventory", TenantId = request.TenantId,
                    UserId = userId, CorrelationId = traceId, ExecutionTime = DateTime.UtcNow
                };
                audit.Actions.Add(new AuditLogActionInfo
                {
                    ServiceName = nameof(AdminInventoryService), MethodName = nameof(ExecuteAsync)
                });
                audit.EntityChanges.Add(new EntityChangeInfo
                {
                    EntityId = request.OrderId, EntityTenantId = request.TenantId,
                    EntityTypeFullName = typeof(InventoryOrderRequest).FullName,
                    ChangeType = EntityChangeType.Created, ChangeTime = audit.ExecutionTime
                });
                var auditId = "inventory-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(new[] { request.TenantId, request.OrderId })))).ToLowerInvariant();
                await _audits.SaveAsync(current, auditId, audit, token).ConfigureAwait(false);
                // 当前回调已经处于 CAP 业务事务中，直接写出箱，不登记第二轮延迟发布。
                var data = new
                {
                    request.TenantId, request.WarehouseId, request.OrderId,
                    Status = completed.Status.ToString(), completed.RemainingQuantities,
                    Lines = request.Lines.Select(line => new { line.Sku, line.Quantity }).ToArray()
                };
                await _events.PublishAsync(new MessageEvent<object>(data)
                {
                    Name = CompletedTopic, Send = true, TenantId = request.TenantId,
                    ContractVersion = "v1", TraceId = traceId
                }, token).ConfigureAwait(false);
                if (_orderedEvents != null)
                {
                    await _orderedEvents.PublishAsync(current, new MessageEvent<object>(data)
                    {
                        Name = CompletedOrderedTopic, Send = true, TenantId = request.TenantId,
                        Stream = OrderedStream, EntityKey = request.OrderId,
                        ContractVersion = "v1", TraceId = traceId
                    }, token).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        });
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("库存工作单元未执行整单事务回调。");
    }
}
