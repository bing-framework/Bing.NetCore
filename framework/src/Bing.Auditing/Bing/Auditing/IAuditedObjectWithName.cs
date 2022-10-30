namespace Bing.Auditing;

/// <summary>
/// 操作审计对象
/// </summary>
public interface IAuditedObjectWithName : ICreationAuditedObjectWithName, IModificationAuditedObjectWithName { }

/// <summary>
/// 操作审计对象
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IAuditedObjectWithName<TKey> : ICreationAuditedObjectWithName<TKey>, IModificationAuditedObjectWithName<TKey> { }