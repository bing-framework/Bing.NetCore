namespace Bing.Auditing
{
    /// <summary>
    /// 操作审计对象
    /// </summary>
    public interface IAuditedObject : ICreationAuditedObject, IModificationAuditedObject { }

    /// <summary>
    /// 操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IAuditedObject<TKey> : ICreationAuditedObject<TKey>, IModificationAuditedObject<TKey> { }
}
