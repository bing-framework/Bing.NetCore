namespace Bing.Auditing;

/// <summary>
/// 定义同时记录创建、修改和软删除信息的审计对象，默认使用可空 GUID 用户标识。
/// </summary>
public interface IFullAuditedObject : IAuditedObject, IDeletionAuditedObject { }

/// <summary>
/// 定义同时记录创建、修改和软删除信息的审计对象。
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IFullAuditedObject<TKey> : IAuditedObject<TKey>, IDeletionAuditedObject<TKey> { }