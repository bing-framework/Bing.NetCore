namespace Bing.Auditing;

/// <summary>
/// 定义记录最后修改时间、修改人标识和修改人名称的审计对象。
/// </summary>
public interface IModificationAuditedObjectWithName : IModificationAuditedObject, IHasModifier { }

/// <summary>
/// 定义使用指定标识类型记录最后修改时间、修改人标识和修改人名称的审计对象。
/// </summary>
/// <typeparam name="TKey">修改人标识类型。</typeparam>
public interface IModificationAuditedObjectWithName<TKey> : IModificationAuditedObject<TKey>, IHasModifier { }