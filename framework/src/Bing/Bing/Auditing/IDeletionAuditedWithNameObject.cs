namespace Bing.Auditing
{
    /// <summary>
    /// 删除操作审计对象
    /// </summary>
    public interface IDeletionAuditedWithNameObject : IDeletionAuditedObject, IHasDeleter { }

    /// <summary>
    /// 删除操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IDeletionAuditedWithNameObject<TKey> : IDeletionAuditedObject<TKey>, IHasDeleter { }
}
