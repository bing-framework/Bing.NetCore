namespace Bing.Auditing;

/// <summary>
/// 定义同时记录创建、修改和软删除信息，并包含人员名称的完整审计对象。
/// </summary>
public interface IFullAuditedObjectWithName : IAuditedObjectWithName, IDeletionAuditedObjectWithName { }

/// <summary>
/// 定义使用指定标识类型记录创建、修改和软删除信息，并包含人员名称的完整审计对象。
/// </summary>
/// <typeparam name="TKey">创建人、修改人和删除人标识类型。</typeparam>
public interface IFullAuditedObjectWithName<TKey> : IAuditedObjectWithName<TKey>, IDeletionAuditedObjectWithName<TKey> { }