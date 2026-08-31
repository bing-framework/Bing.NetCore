namespace Bing.Auditing;

/// <summary>
/// 定义使用可空 GUID 用户标识记录创建时间和创建人的审计对象。
/// </summary>
public interface ICreationAuditedObject : ICreationAuditedObject<Guid?> { }

/// <summary>
/// 定义使用指定标识类型记录创建时间和创建人的审计对象。
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface ICreationAuditedObject<TKey> : IHasCreationTime
{
    /// <summary>
    /// 获取或设置创建该实体的用户或主体标识。
    /// </summary>
    TKey CreatorId { get; set; }
}
