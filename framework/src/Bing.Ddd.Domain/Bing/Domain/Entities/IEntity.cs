namespace Bing.Domain.Entities;

/// <summary>
/// 定义领域实体的初始化和复合标识访问能力。
/// </summary>
public interface IEntity : IDomainObject
{
    /// <summary>
    /// 初始化实体状态。
    /// </summary>
    void Init();

    /// <summary>
    /// 获取用于实体相等性判断的标识值列表。
    /// </summary>
    /// <returns>按实体标识组成顺序返回的键值数组。</returns>
    object[] GetKeys();
}

/// <summary>
/// 定义具有单一标识的领域实体。
/// </summary>
/// <typeparam name="TKey">可协变返回的实体标识类型。</typeparam>
public interface IEntity<out TKey> : IKey<TKey>, IEntity { }

/// <summary>
/// 定义带变更跟踪能力的旧版单一标识实体。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
/// <typeparam name="TKey">可协变返回的实体标识类型。</typeparam>
[Obsolete("已弃用，无需变更跟踪")]
public interface IEntity<in TEntity, out TKey> : ChangeTracking.IChangeTrackable, IEntity<TKey> where TEntity : IEntity { }
