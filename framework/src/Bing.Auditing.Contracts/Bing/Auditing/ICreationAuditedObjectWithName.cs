namespace Bing.Auditing;

/// <summary>
/// 定义记录创建时间、创建人标识和创建人名称的审计对象。
/// </summary>
public interface ICreationAuditedObjectWithName : ICreationAuditedObject, IHasCreator { }

/// <summary>
/// 定义使用指定标识类型记录创建时间、创建人标识和创建人名称的审计对象。
/// </summary>
/// <typeparam name="TKey">创建人标识类型。</typeparam>
public interface ICreationAuditedObjectWithName<TKey> : ICreationAuditedObject<TKey>, IHasCreator { }