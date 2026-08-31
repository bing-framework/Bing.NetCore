namespace Bing.Auditing;

/// <summary>
/// 定义使用可空 GUID 用户标识记录最后修改时间和修改人的审计对象。
/// </summary>
public interface IModificationAuditedObject : IModificationAuditedObject<Guid?> { }

/// <summary>
/// 定义使用指定标识类型记录最后修改时间和修改人的审计对象。
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IModificationAuditedObject<TKey> : IHasModificationTime
{
    /// <summary>
    /// 获取或设置最后修改该实体的用户或主体标识。
    /// </summary>
    TKey LastModifierId { get; set; }
}
