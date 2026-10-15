using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bing.Data.Inventory;

/// <summary>
/// 描述同一租户和仓库的一次整单库存请求。
/// </summary>
public sealed class InventoryOrderRequest
{
    /// <summary>
    /// 单次请求允许的最大库存明细数。
    /// </summary>
    private const int MaxLines = 1000;

    /// <summary>
    /// 拒绝无效代理字符的身份编码器。
    /// </summary>
    private static readonly Encoding IdentityEncoding = new UTF8Encoding(false, true);

    /// <summary>
    /// 初始化一个 <see cref="InventoryOrderRequest" /> 类型的实例。
    /// </summary>
    /// <param name="tenantId">租户标识。</param>
    /// <param name="warehouseId">仓库标识。</param>
    /// <param name="orderId">租户内唯一的业务请求标识。</param>
    /// <param name="lines">库存明细；SKU 不能重复。</param>
    public InventoryOrderRequest(string tenantId, string warehouseId, string orderId,
        IEnumerable<InventoryOrderLine> lines)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines));
        var snapshot = lines.Take(MaxLines + 1).ToArray();
        if (snapshot.Length == 0 || snapshot.Length > MaxLines || snapshot.Any(line => line == null))
            throw new ArgumentException("整单必须包含 1-1000 个非空库存明细。", nameof(lines));
        var identity = new InventoryDeductionRequest(tenantId, warehouseId, snapshot[0].Sku,
            snapshot[0].Quantity, orderId);
        TenantId = identity.TenantId;
        WarehouseId = identity.WarehouseId;
        OrderId = identity.RequestId;
        snapshot = snapshot.OrderBy(line => line.Sku, StringComparer.Ordinal).ToArray();
        foreach (var key in new[] { TenantId, WarehouseId, OrderId }.Concat(snapshot.Select(line => line.Sku)))
            IdentityEncoding.GetByteCount(key);
        if (snapshot.Select(line => line.Sku).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new ArgumentException("整单不能包含重复 SKU。", nameof(lines));
        Lines = Array.AsReadOnly(snapshot);
        var content = new JArray(TenantId, WarehouseId,
            new JArray(snapshot.Select(line => new JArray(line.Sku,
                line.Quantity.ToString(CultureInfo.InvariantCulture)))));
        Fingerprint = Hash(content.ToString(Formatting.None));
    }

    /// <summary>
    /// 获取租户标识。
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// 获取仓库标识。
    /// </summary>
    public string WarehouseId { get; }

    /// <summary>
    /// 获取整单请求标识。
    /// </summary>
    public string OrderId { get; }

    /// <summary>
    /// 获取按 SKU 的 Ordinal 顺序排列的明细快照。
    /// </summary>
    public IReadOnlyList<InventoryOrderLine> Lines { get; }

    /// <summary>
    /// 获取用于核对请求内容的摘要。
    /// </summary>
    internal string Fingerprint { get; }

    /// <summary>
    /// 创建整单内部的单项扣减请求。
    /// </summary>
    /// <param name="index">有序明细索引。</param>
    /// <returns>具有整单专属请求标识的库存请求。</returns>
    internal InventoryDeductionRequest CreateDeduction(int index) => new(TenantId, WarehouseId,
        Lines[index].Sku, Lines[index].Quantity,
        "order-" + Hash(new JArray(TenantId, OrderId, index).ToString(Formatting.None)));

    /// <summary>
    /// 计算无歧义序列化内容的固定摘要。
    /// </summary>
    /// <param name="value">已序列化的请求身份或内容。</param>
    /// <returns>小写十六进制 SHA-256 摘要。</returns>
    private static string Hash(string value)
    {
        using var hash = SHA256.Create();
        return string.Concat(hash.ComputeHash(IdentityEncoding.GetBytes(value))
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }
}

/// <summary>
/// 描述一项正数数量的 SKU 扣减。
/// </summary>
public sealed class InventoryOrderLine
{
    /// <summary>
    /// 初始化一个 <see cref="InventoryOrderLine" /> 类型的实例。
    /// </summary>
    /// <param name="sku">SKU 标识。</param>
    /// <param name="quantity">大于零的扣减数量。</param>
    public InventoryOrderLine(string sku, long quantity)
    {
        var validated = new InventoryDeductionRequest("validation", "validation", sku, quantity, "validation");
        Sku = validated.Sku;
        Quantity = validated.Quantity;
    }

    /// <summary>
    /// 获取 SKU 标识。
    /// </summary>
    public string Sku { get; }

    /// <summary>
    /// 获取扣减数量。
    /// </summary>
    public long Quantity { get; }
}
