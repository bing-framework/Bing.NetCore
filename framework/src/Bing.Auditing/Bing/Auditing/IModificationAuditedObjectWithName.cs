namespace Bing.Auditing
{
    /// <summary>
    /// 修改操作审计对象
    /// </summary>
    public interface IModificationAuditedObjectWithName : IModificationAuditedObject, IHasModifier { }

    /// <summary>
    /// 修改操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IModificationAuditedObjectWithName<TKey> : IModificationAuditedObject<TKey>, IHasModifier { }
}
