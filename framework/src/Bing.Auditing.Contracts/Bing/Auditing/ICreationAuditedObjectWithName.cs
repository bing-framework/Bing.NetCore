namespace Bing.Auditing;

/// <summary>
/// 创建操作审计对象
/// </summary>
public interface ICreationAuditedObjectWithName : ICreationAuditedObject, IHasCreator { }

/// <summary>
/// 创建操作审计对象
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface ICreationAuditedObjectWithName<TKey> : ICreationAuditedObject<TKey>, IHasCreator { }