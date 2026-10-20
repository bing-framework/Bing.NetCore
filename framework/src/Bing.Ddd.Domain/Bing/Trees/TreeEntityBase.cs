using Bing.Domain.Entities;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Validation;

namespace Bing.Trees;

/// <summary>
/// 提供使用默认 GUID 标识和父标识的树形实体基类。
/// </summary>
/// <typeparam name="TEntity">具体树形实体类型。</typeparam>
public abstract class TreeEntityBase<TEntity> : TreeEntityBase<TEntity, Guid, Guid?>
    where TEntity : class, ITreeEntity<TEntity, Guid, Guid?>, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化一个 <see cref="TreeEntityBase{TEntity}"/> 类型的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    /// <param name="path">实体物化路径。</param>
    /// <param name="level">实体在树中的层级。</param>
    protected TreeEntityBase(Guid id, string path, int level)
        : base(id, path, level)
    {
    }
}

/// <summary>
/// 提供包含父节点、物化路径、层级、排序和启用状态的树形实体基类。
/// </summary>
/// <typeparam name="TEntity">具体树形实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
/// <typeparam name="TParentId">父节点标识类型。</typeparam>
public abstract class TreeEntityBase<TEntity, TKey, TParentId> : BasicAggregateRoot<TEntity, TKey>,
    ITreeEntity<TEntity, TKey, TParentId>
    where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>, IVerifyModel<TEntity>
{
    /// <inheritdoc />
    public TParentId ParentId { get; set; }

    /// <inheritdoc />
    public string Path { get; protected set; }

    /// <inheritdoc />
    public int Level { get; protected set; }

    /// <inheritdoc />
    /// <remarks>
    /// 为空时不指定排序号。
    /// </remarks>
    public int? SortId { get; set; }

    /// <inheritdoc />
    public bool Enabled { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="TreeEntityBase{TEntity, TKey, TParentId}"/> 类型的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    /// <param name="path">实体物化路径。</param>
    /// <param name="level">实体在树中的层级。</param>
    protected TreeEntityBase(TKey id, string path, int level) : base(id)
    {
        Path = path;
        Level = level;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 根据当前实体标识初始化根节点路径。
    /// </remarks>
    public virtual void InitPath() => InitPath(default);

    /// <inheritdoc />
    /// <remarks>
    /// 根据父节点初始化当前实体的物化路径和层级。
    /// </remarks>
    public void InitPath(TEntity parent)
    {
        if (Equals(parent, null))
        {
            Level = 1;
            Path = $"{Id},";
            return;
        }

        Level = parent.Level + 1;
        Path = $"{parent.Path}{Id},";
    }

    /// <inheritdoc />
    /// <remarks>
    /// 从当前物化路径中获取所有上级节点标识。
    /// </remarks>
    public List<TKey> GetParentIdsFromPath(bool excludeSelf = true)
    {
        if (string.IsNullOrWhiteSpace(Path))
            return new List<TKey>();
        var result = Path.Split(',').Where(id => !string.IsNullOrWhiteSpace(id) && id != ",").ToList();
        if (excludeSelf)
            result = result.Where(id => id.SafeString().ToLower() != Id.SafeString().ToLower()).ToList();
        return result.Select(Conv.To<TKey>).ToList();
    }
}
