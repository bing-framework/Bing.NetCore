namespace Bing.Auditing;

/// <summary>
/// 删除操作审计对象
/// </summary>
public interface IDeletionAuditedObject : IDeletionAuditedObject<Guid?> { }

/// <summary>
/// 删除操作审计对象
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IDeletionAuditedObject<TKey> : IHasDeletionTime
{
    /// <summary>
    /// 删除人标识
    /// </summary>
    TKey DeleterId { get; set; }
}
