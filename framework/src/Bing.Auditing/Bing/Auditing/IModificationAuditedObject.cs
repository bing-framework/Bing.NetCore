using System;

namespace Bing.Auditing;

/// <summary>
/// 修改操作审计对象
/// </summary>
public interface IModificationAuditedObject : IModificationAuditedObject<Guid?> { }

/// <summary>
/// 修改操作审计对象
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IModificationAuditedObject<TKey> : IHasModificationTime
{
    /// <summary>
    /// 最后修改人标识
    /// </summary>
    TKey LastModifierId { get; set; }
}