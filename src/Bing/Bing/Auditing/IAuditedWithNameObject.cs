namespace Bing.Auditing
{
    /// <summary>
    /// 操作审计对象
    /// </summary>
    public interface IAuditedWithNameObject : ICreationAuditedWithNameObject, IModificationAuditedWithNameObject { }

    /// <summary>
    /// 操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IAuditedWithNameObject<TKey> : ICreationAuditedWithNameObject<TKey>, IModificationAuditedWithNameObject<TKey> { }
}
