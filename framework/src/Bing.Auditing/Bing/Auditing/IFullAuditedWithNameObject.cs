namespace Bing.Auditing
{
    /// <summary>
    /// 完整操作审计对象
    /// </summary>
    public interface IFullAuditedWithNameObject : IAuditedWithNameObject, IDeletionAuditedWithNameObject { }

    /// <summary>
    /// 完整操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IFullAuditedWithNameObject<TKey> : IAuditedWithNameObject<TKey>, IDeletionAuditedWithNameObject<TKey> { }
}
