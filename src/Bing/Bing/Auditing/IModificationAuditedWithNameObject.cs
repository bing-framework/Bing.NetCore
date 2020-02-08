namespace Bing.Auditing
{
    /// <summary>
    /// 修改操作审计对象
    /// </summary>
    public interface IModificationAuditedWithNameObject : IModificationAuditedObject, IHasModifier { }

    /// <summary>
    /// 修改操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IModificationAuditedWithNameObject<TKey> : IModificationAuditedObject<TKey>, IHasModifier { }
}
