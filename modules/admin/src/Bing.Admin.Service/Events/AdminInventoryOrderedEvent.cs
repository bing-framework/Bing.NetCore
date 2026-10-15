using System;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Data.Inventory;
using Bing.Data.Events.Ordering;
using Bing.Data.Inventory;
using Bing.Events.Ordering;
using DotNetCore.CAP;

namespace Bing.Admin.Service.Events;

/// <summary>
/// 接收 Admin 库存完成通知并写入有序收件箱。
/// </summary>
/// <remarks>CAP 主题和消费者组固定在代码中，版本切换时保持逻辑消费者名称不变。</remarks>
public sealed class AdminInventoryOrderedEventSubscriber : ICapSubscribe
{
    /// <summary>
    /// 有序入箱适配器。
    /// </summary>
    private readonly CapOrderedEventIngress _ingress;

    /// <summary>
    /// 初始化一个 <see cref="AdminInventoryOrderedEventSubscriber" /> 类型的实例。
    /// </summary>
    /// <param name="ingress">有序 CAP 入箱适配器。</param>
    public AdminInventoryOrderedEventSubscriber(CapOrderedEventIngress ingress)
    {
        _ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
    }

    /// <summary>
    /// 将 CAP 库存完成消息写入有序收件箱。
    /// </summary>
    /// <param name="payload">CAP 反序列化后的 JSON 正文。</param>
    /// <param name="headers">CAP 消息头。</param>
    /// <returns>表示入箱事务完成的异步操作。</returns>
    [CapSubscribe(AdminInventoryService.CompletedOrderedTopic, Group = "bing-admin-inventory-ordered-ingress")]
    public Task ReceiveAsync(JsonElement payload, [FromCap] CapHeader headers) =>
        _ingress.ReceiveAsync(AdminInventoryService.OrderedConsumer, headers, payload.GetRawText());
}

/// <summary>
/// 在有序处理事务中保存 Admin 库存完成投影。
/// </summary>
/// <remarks>处理器不重新调用库存工作单元，避免创建第二个事务或重复扣减库存。</remarks>
public sealed class AdminInventoryOrderedEventHandler : IOrderedEventHandler
{
    /// <summary>
    /// 库存投影正文的最大 UTF-8 字节数。
    /// </summary>
    private const int MaxPayloadBytes = 65536;

    /// <summary>
    /// 有序库存投影存储。
    /// </summary>
    private readonly IInventoryOrderProjectionStore _projections;

    /// <summary>
    /// 初始化一个 <see cref="AdminInventoryOrderedEventHandler" /> 类型的实例。
    /// </summary>
    /// <param name="projections">库存完成投影存储。</param>
    public AdminInventoryOrderedEventHandler(IInventoryOrderProjectionStore projections)
    {
        _projections = projections ?? throw new ArgumentNullException(nameof(projections));
    }

    /// <inheritdoc />
    public Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
        DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (!string.Equals(stream.Stream, AdminInventoryService.OrderedStream, StringComparison.Ordinal))
            throw new InvalidOperationException("库存有序事件的业务流不匹配。");
        if (!string.Equals(message.ContractVersion, "v1", StringComparison.Ordinal))
            throw new InvalidOperationException("库存有序事件契约版本不受支持。");
        if (Encoding.UTF8.GetByteCount(message.Payload) > MaxPayloadBytes)
            throw new ArgumentException("库存事件正文不能超过 65536 字节。", nameof(message));

        using var document = JsonDocument.Parse(message.Payload);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new FormatException("库存有序事件正文必须是 JSON 对象。");
        var tenantId = GetRequiredString(root, "TenantId");
        var orderId = GetRequiredString(root, "OrderId");
        var status = GetRequiredString(root, "Status");
        _ = GetRequiredString(root, "WarehouseId");
        if (!string.Equals(tenantId, stream.TenantId, StringComparison.Ordinal))
            throw new InvalidOperationException("库存有序事件租户与业务流不匹配。");
        if (!string.Equals(orderId, stream.EntityKey, StringComparison.Ordinal))
            throw new InvalidOperationException("库存有序事件订单与业务流不匹配。");
        if (!string.Equals(status, "Applied", StringComparison.Ordinal) &&
            !string.Equals(status, "Insufficient", StringComparison.Ordinal))
            throw new FormatException("库存有序事件状态无效。");

        return _projections.SaveAsync(transaction, AdminInventoryService.OrderedConsumer,
            stream, message, status, message.Payload, cancellationToken);
    }

    /// <summary>
    /// 读取必需的 JSON 文本属性。
    /// </summary>
    /// <param name="root">JSON 根对象。</param>
    /// <param name="name">属性名称。</param>
    /// <returns>非空属性值。</returns>
    private static string GetRequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new FormatException($"库存有序事件缺少有效的 {name} 属性。");
        return value.GetString();
    }
}
