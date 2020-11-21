namespace Bing.Auditing
{
    /// <summary>
    /// 完整操作审计对象
    /// </summary>
    public interface IFullAuditedObjectWithName : IAuditedObjectWithName, IDeletionAuditedObjectWithName { }

    /// <summary>
    /// 完整操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IFullAuditedObjectWithName<TKey> : IAuditedObjectWithName<TKey>, IDeletionAuditedObjectWithName<TKey> { }
}
