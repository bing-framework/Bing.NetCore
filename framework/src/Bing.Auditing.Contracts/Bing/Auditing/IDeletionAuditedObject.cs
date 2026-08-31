namespace Bing.Auditing;

/// <summary>
/// 定义使用可空 GUID 用户标识记录软删除时间和删除人的审计对象。
/// </summary>
public interface IDeletionAuditedObject : IDeletionAuditedObject<Guid?> { }

/// <summary>
/// 定义使用指定标识类型记录软删除时间和删除人的审计对象。
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IDeletionAuditedObject<TKey> : IHasDeletionTime
{
    /// <summary>
    /// 获取或设置删除该实体的用户或主体标识。
    /// </summary>
    TKey DeleterId { get; set; }
}
