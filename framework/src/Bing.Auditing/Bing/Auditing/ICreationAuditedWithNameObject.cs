namespace Bing.Auditing
{
    /// <summary>
    /// 创建操作审计对象
    /// </summary>
    public interface ICreationAuditedWithNameObject : ICreationAuditedObject, IHasCreator { }

    /// <summary>
    /// 创建操作审计对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface ICreationAuditedWithNameObject<TKey> : ICreationAuditedObject<TKey>, IHasCreator { }
}
