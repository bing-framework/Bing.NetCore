namespace Bing.Auditing;

/// <summary>
/// 删除操作审计对象
/// </summary>
public interface IDeletionAuditedObjectWithName : IDeletionAuditedObject, IHasDeleter { }

/// <summary>
/// 删除操作审计对象
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IDeletionAuditedObjectWithName<TKey> : IDeletionAuditedObject<TKey>, IHasDeleter { }