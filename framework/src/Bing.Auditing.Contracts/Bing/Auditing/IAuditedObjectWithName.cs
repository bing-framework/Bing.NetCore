namespace Bing.Auditing;

/// <summary>
/// 定义同时记录创建和最后修改信息，并包含人员名称的审计对象。
/// </summary>
public interface IAuditedObjectWithName : ICreationAuditedObjectWithName, IModificationAuditedObjectWithName { }

/// <summary>
/// 定义使用指定标识类型记录创建和最后修改信息，并包含人员名称的审计对象。
/// </summary>
/// <typeparam name="TKey">创建人和修改人标识类型。</typeparam>
public interface IAuditedObjectWithName<TKey> : ICreationAuditedObjectWithName<TKey>, IModificationAuditedObjectWithName<TKey> { }