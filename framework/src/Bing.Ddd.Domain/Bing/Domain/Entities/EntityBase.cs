using System.ComponentModel.DataAnnotations;
using Bing.Extensions;
using Bing.Properties;
using Bing.Validation;

namespace Bing.Domain.Entities;

/// <summary>
/// 为领域实体提供基础身份和相等性支持。
/// </summary>
[Serializable]
public abstract class EntityBase : DomainObjectBase, IEntity
{
    /// <inheritdoc />
    public abstract void Init();

    /// <inheritdoc />
    public abstract object[] GetKeys();

    /// <summary>
    /// 返回包含实体类型和标识值的诊断文本。
    /// </summary>
    /// <returns>实体的诊断文本表示。</returns>
    public override string ToString() => $"[Entity: {GetType().Name}] Keys = {GetKeys().Join(", ")}";

    /// <summary>
    /// 确定当前实体是否等于另一个实体。
    /// </summary>
    /// <param name="other">要比较的实体对象。</param>
    /// <returns>
    /// 如果两个实体相等，则返回 <c>true</c>；否则返回 <c>false</c>。
    /// </returns>
    public bool EntityEquals(IEntity other) => EntityHelper.EntityEquals(this, other);
}

/// <summary>
/// 为以 <see cref="Guid"/> 为默认标识的领域实体提供基类。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
[Serializable]
public abstract class EntityBase<TEntity> : EntityBase<TEntity, Guid>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化 <see cref="EntityBase{TEntity}"/> 的实例。
    /// </summary>
    protected EntityBase() { }

    /// <summary>
    /// 使用标识初始化 <see cref="EntityBase{TEntity}"/> 的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    protected EntityBase(Guid id) : base(id) { }
}

/// <summary>
/// 为具有单一标识的领域实体提供基础实现。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
[Serializable]
public abstract class EntityBase<TEntity, TKey> : DomainObjectBase<TEntity>, IEntity<TEntity, TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <inheritdoc />
    /// <remarks>派生实体可在受保护上下文中设置标识。</remarks>
    [Key, Required(ErrorMessageResourceType = typeof(R), ErrorMessageResourceName = "IdIsEmpty")]
    public virtual TKey Id { get; protected set; }

    /// <summary>
    /// 初始化 <see cref="EntityBase{TEntity,TKey}"/> 的实例。
    /// </summary>
    protected EntityBase() { }

    /// <summary>
    /// 使用标识初始化 <see cref="EntityBase{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    protected EntityBase(TKey id) => Id = id;

    /// <summary>
    /// 判断当前实体是否与另一个对象表示同一数据记录。
    /// </summary>
    /// <param name="other">要比较的对象。</param>
    /// <returns>两个对象表示同一非默认标识的实体时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public override bool Equals(object other) => this == (other as EntityBase<TEntity, TKey>);

    /// <summary>
    /// 用作特定类型的哈希函数。
    /// </summary>
    /// <returns>基于实体标识计算的哈希代码。</returns>
    public override int GetHashCode() => ReferenceEquals(Id, null) ? 0 : Id.GetHashCode();

    /// <summary>
    /// 比较两个实体是否具有相同的非默认标识。
    /// </summary>
    /// <param name="left">左侧实体。</param>
    /// <param name="right">右侧实体。</param>
    /// <returns>两个实体具有相同非默认标识时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool operator ==(EntityBase<TEntity, TKey> left, EntityBase<TEntity, TKey> right)
    {
        if ((object)left == null && (object)right == null)
            return true;
        if (!(left is TEntity) || !(right is TEntity))
            return false;
        if (Equals(left.Id, null))
            return false;
        if (left.Id.Equals(default(TKey)))
            return false;
        return left.Id.Equals(right.Id);
    }

    /// <summary>
    /// 比较两个实体是否不具有相同的非默认标识。
    /// </summary>
    /// <param name="left">左侧实体。</param>
    /// <param name="right">右侧实体。</param>
    /// <returns>两个实体不相等时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool operator !=(EntityBase<TEntity, TKey> left, EntityBase<TEntity, TKey> right) => !(left == right);

    /// <inheritdoc />
    /// <remarks>默认实现仅在支持的标识类型上初始化缺失标识。</remarks>
    public virtual void Init() => InitId();

    /// <inheritdoc />
    public object[] GetKeys() => new object[] { Id };

    /// <summary>
    /// 初始化缺失的实体标识。
    /// </summary>
    protected virtual void InitId()
    {
        // TODO: 考虑跳过该判断方法
        if (typeof(TKey) == typeof(int) || typeof(TKey) == typeof(long))
            return;
        if (string.IsNullOrWhiteSpace(Id.SafeString()) || Id.Equals(default(TKey)))
            Id = CreateId();
    }

    /// <summary>
    /// 创建新的实体标识。
    /// </summary>
    /// <returns>为当前实体生成的标识。</returns>
    protected virtual TKey CreateId() => EntityHelper.CreateKey<TKey>();

    /// <summary>
    /// 验证实体状态。
    /// </summary>
    /// <param name="results">用于收集验证失败信息的结果集合。</param>
    protected override void Validate(ValidationResultCollection results) => ValidateId(results);

    /// <summary>
    /// 验证实体标识是否有效。
    /// </summary>
    /// <param name="results">用于收集标识验证失败信息的结果集合。</param>
    protected virtual void ValidateId(ValidationResultCollection results)
    {
        if (typeof(TKey) == typeof(int) || typeof(TKey) == typeof(long))
            return;
        if (string.IsNullOrWhiteSpace(Id.SafeString()) || Id.Equals(default(TKey)))
            results.Add(new ValidationResult(R.IdIsEmpty));
    }
}
