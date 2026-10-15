namespace Bing.Data.Inventory;

/// <summary>
/// 表示一次库存扣减的持久化结果。
/// </summary>
public sealed class InventoryDeductionResult
{
    /// <summary>
    /// 初始化一个 <see cref="InventoryDeductionResult" /> 类型的实例。
    /// </summary>
    public InventoryDeductionResult(InventoryDeductionStatus status, bool duplicate, long? remainingQuantity)
    {
        Status = status;
        Duplicate = duplicate;
        RemainingQuantity = remainingQuantity;
    }

    /// <summary>
    /// 扣减处理状态。
    /// </summary>
    public InventoryDeductionStatus Status { get; }

    /// <summary>
    /// 是否来自已提交的相同请求。
    /// </summary>
    public bool Duplicate { get; }

    /// <summary>
    /// 扣减后的剩余数量；扣减失败且库存行不存在时为空。
    /// </summary>
    public long? RemainingQuantity { get; }

    /// <summary>
    /// 是否成功扣减。
    /// </summary>
    public bool Applied => Status == InventoryDeductionStatus.Applied;
}

/// <summary>
/// 库存扣减结果状态。
/// </summary>
public enum InventoryDeductionStatus
{
    /// <summary>
    /// 已完成扣减。
    /// </summary>
    Applied,

    /// <summary>
    /// 余额不足或库存行不存在。
    /// </summary>
    Insufficient,

    /// <summary>
    /// 请求记录已存在但尚未完成。
    /// </summary>
    Pending
}
