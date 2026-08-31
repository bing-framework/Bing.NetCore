using System.ComponentModel.DataAnnotations;
using Bing.Domain.Entities;

namespace Bing.Data.Persistence;

/// <summary>
/// 提供使用 GUID 标识的持久化实体基类。
/// </summary>
public abstract class PersistentEntityBase : PersistentEntityBase<Guid> { }

/// <summary>
/// 提供具有泛型标识和实体相等性语义的持久化实体基类。
/// </summary>
/// <typeparam name="TKey">实体标识类型。</typeparam>
public abstract class PersistentEntityBase<TKey> : IKey<TKey>
{
    /// <summary>
    /// 获取或设置实体的持久化标识。
    /// </summary>
    [Key]
    public TKey Id { get; set; }

    /// <summary>
    /// 判断当前实体是否与指定对象表示同一个持久化实体。
    /// </summary>
    /// <param name="other">要比较的对象。</param>
    /// <returns>对象类型和非默认标识均相同，且标识值相等时返回 <see langword="true"/>。</returns>
    public override bool Equals(object other) => this == (PersistentEntityBase<TKey>)other;

    /// <summary>
    /// 返回基于实体标识的哈希代码；标识为空时返回 0。
    /// </summary>
    /// <returns>基于实体标识计算的哈希代码。</returns>
    public override int GetHashCode() => ReferenceEquals(Id, null) ? 0 : Id.GetHashCode();

    /// <summary>
    /// 比较两个实体是否表示同一个持久化实体。
    /// </summary>
    /// <param name="left">左侧实体。</param>
    /// <param name="right">右侧实体。</param>
    /// <returns>两个实体的具体类型和非默认标识均相同，且标识值相等时返回 <see langword="true"/>。</returns>
    public static bool operator ==(PersistentEntityBase<TKey> left, PersistentEntityBase<TKey> right)
    {
        if ((object)left == null && (object)right == null)
            return true;
        if ((object)left == null || (object)right == null)
            return false;
        if (left.GetType() != right.GetType())
            return false;
        if (Equals(left.Id, null))
            return false;
        if (left.Id.Equals(default(TKey)))
            return false;
        return left.Id.Equals(right.Id);
    }

    /// <summary>
    /// 比较两个实体是否表示不同的持久化实体。
    /// </summary>
    /// <param name="left">左侧实体。</param>
    /// <param name="right">右侧实体。</param>
    /// <returns>两个实体不满足相等条件时返回 <see langword="true"/>。</returns>
    public static bool operator !=(PersistentEntityBase<TKey> left, PersistentEntityBase<TKey> right) => !(left == right);
}