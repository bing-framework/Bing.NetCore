using System.Text;

namespace Bing.Data.Inventory;

/// <summary>
/// 描述一次租户、仓库和 SKU 范围内的库存扣减请求。
/// </summary>
/// <remarks>身份保持大小写、尾空格和 Unicode 原值；字符长度按 UTF-16 计数，拒绝无法无损编码的代理字符。</remarks>
public sealed class InventoryDeductionRequest
{
    /// <summary>
    /// 校验身份能无损编码为 UTF-8 的编码器。
    /// </summary>
    private static readonly Encoding IdentityEncoding = new UTF8Encoding(false, true);

    /// <summary>
    /// 初始化一个 <see cref="InventoryDeductionRequest" /> 类型的实例。
    /// </summary>
    /// <param name="tenantId">租户标识。</param>
    /// <param name="warehouseId">仓库标识。</param>
    /// <param name="sku">SKU 标识。</param>
    /// <param name="quantity">扣减数量。</param>
    /// <param name="requestId">业务请求唯一标识。</param>
    public InventoryDeductionRequest(string tenantId, string warehouseId, string sku, long quantity,
        string requestId)
    {
        TenantId = ValidateKey(tenantId, 128, nameof(tenantId));
        WarehouseId = ValidateKey(warehouseId, 128, nameof(warehouseId));
        Sku = ValidateKey(sku, 256, nameof(sku));
        RequestId = ValidateKey(requestId, 128, nameof(requestId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "库存扣减数量必须大于零。");
        Quantity = quantity;
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
    /// 获取 SKU 标识。
    /// </summary>
    public string Sku { get; }

    /// <summary>
    /// 获取扣减数量。
    /// </summary>
    public long Quantity { get; }

    /// <summary>
    /// 获取业务请求唯一标识。
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// 验证库存身份。
    /// </summary>
    /// <param name="value">待验证的原始标识。</param>
    /// <param name="maxLength">允许的最大 UTF-16 长度。</param>
    /// <param name="name">用于异常诊断的参数名。</param>
    /// <returns>能够无损编码且长度有效的原标识。</returns>
    private static string ValidateKey(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("库存标识不能为空。", name);
        if (value.Length > maxLength)
            throw new ArgumentOutOfRangeException(name, $"库存标识不能超过 {maxLength} 个字符。");
        IdentityEncoding.GetByteCount(value);
        return value;
    }
}
