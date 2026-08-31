namespace Bing.Domain.Entities;

/// <summary>
/// 表示领域聚合的根实体。
/// </summary>
/// <remarks>聚合外部应通过聚合根访问和维护聚合内部状态。</remarks>
public interface IAggregateRoot : IEntity;

/// <summary>
/// 表示具有指定标识类型的聚合根。
/// </summary>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
public interface IAggregateRoot<out TKey> : IEntity<TKey>, IAggregateRoot;

/// <summary>
/// 表示具有实体自引用类型和指定标识类型的聚合根。
/// </summary>
/// <typeparam name="TEntity">聚合根的具体实体类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
public interface IAggregateRoot<in TEntity, out TKey> : IEntity<TEntity, TKey>, IAggregateRoot<TKey>
    where TEntity : IAggregateRoot;
