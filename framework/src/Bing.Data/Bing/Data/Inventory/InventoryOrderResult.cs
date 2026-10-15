namespace Bing.Data.Inventory;

/// <summary>
/// 表示整单库存请求的首次持久化结果。
/// </summary>
public sealed class InventoryOrderResult
{
    /// <summary>
    /// 初始化一个 <see cref="InventoryOrderResult" /> 类型的实例。
    /// </summary>
    /// <param name="status">已完成扣减或余额不足。</param>
    /// <param name="duplicate">是否读取首次结果。</param>
    /// <param name="remainingQuantities">成功时按请求明细顺序保存的余额；不足时为空。</param>
    public InventoryOrderResult(InventoryDeductionStatus status, bool duplicate,
        IEnumerable<long?> remainingQuantities)
    {
        if (status != InventoryDeductionStatus.Applied && status != InventoryDeductionStatus.Insufficient)
            throw new ArgumentOutOfRangeException(nameof(status));
        if (remainingQuantities == null)
            throw new ArgumentNullException(nameof(remainingQuantities));
        Status = status;
        Duplicate = duplicate;
        RemainingQuantities = Array.AsReadOnly(remainingQuantities.ToArray());
    }

    /// <summary>
    /// 获取整单处理结果。
    /// </summary>
    public InventoryDeductionStatus Status { get; }

    /// <summary>
    /// 获取是否读取首次持久化结果。
    /// </summary>
    public bool Duplicate { get; }

    /// <summary>
    /// 获取成功扣减后的余额快照。
    /// </summary>
    public IReadOnlyList<long?> RemainingQuantities { get; }

    /// <summary>
    /// 获取整单是否成功扣减。
    /// </summary>
    public bool Applied => Status == InventoryDeductionStatus.Applied;
}
