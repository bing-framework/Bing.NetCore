namespace Bing.Auditing
{
    /// <summary>
    /// 完整操作审计对象
    /// </summary>
    public interface IFullAuditedObject : IAuditedObject, IDeletionAuditedObject { }

    /// <summary>
    /// 完整操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IFullAuditedObject<TKey> : IAuditedObject<TKey>, IDeletionAuditedObject<TKey> { }
}
