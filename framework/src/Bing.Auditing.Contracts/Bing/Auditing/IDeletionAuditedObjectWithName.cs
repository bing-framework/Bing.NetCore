namespace Bing.Auditing;

/// <summary>
/// 定义记录软删除时间、删除人标识和删除人名称的审计对象。
/// </summary>
public interface IDeletionAuditedObjectWithName : IDeletionAuditedObject, IHasDeleter { }

/// <summary>
/// 定义使用指定标识类型记录软删除时间、删除人标识和删除人名称的审计对象。
/// </summary>
/// <typeparam name="TKey">删除人标识类型。</typeparam>
public interface IDeletionAuditedObjectWithName<TKey> : IDeletionAuditedObject<TKey>, IHasDeleter { }