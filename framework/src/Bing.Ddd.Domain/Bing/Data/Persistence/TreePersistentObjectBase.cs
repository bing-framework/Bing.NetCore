using Bing.Trees;

namespace Bing.Data.Persistence;

/// <summary>
/// 提供使用默认 GUID 标识和父标识的树形持久化对象基类。
/// </summary>
public abstract class TreePersistentObjectBase : TreePersistentObjectBase<Guid, Guid?>
{
}

/// <summary>
/// 提供包含父节点、物化路径、层级、启用状态和排序号的树形持久化对象基类。
/// </summary>
/// <typeparam name="TKey">节点标识类型。</typeparam>
/// <typeparam name="TParentId">父节点标识类型。</typeparam>
public abstract class TreePersistentObjectBase<TKey, TParentId> : PersistentObjectBase<TKey>, IParentId<TParentId>, IPath, IEnabled, ISortId
{
    /// <inheritdoc />
    public TParentId ParentId { get; set; }

    /// <inheritdoc />
    public virtual string Path { get; set; }

    /// <inheritdoc />
    public int Level { get; set; }

    /// <inheritdoc />
    public bool Enabled { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 为空时不指定排序号。
    /// </remarks>
    public int? SortId { get; set; }
}