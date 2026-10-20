using Bing.Domain.Entities;
using Bing.Auditing;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Domain.Entities.Events;
using IUnitOfWork = Bing.Uow.IUnitOfWork;

namespace Bing.Data.Stores;

/// <summary>
/// 存储器
/// </summary>
/// <typeparam name="TEntity">对象类型</typeparam>
public abstract class StoreBase<TEntity> : StoreBase<TEntity, Guid>, IStore<TEntity>
    where TEntity : class, IKey<Guid>, IVersion
{
    /// <summary>
    /// 初始化一个 <see cref="StoreBase{TEntity}" /> 类型的实例。
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    protected StoreBase(IUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
        IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
        ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
        metadataOptions, typeConverterResolver)
    {
    }
}

/// <summary>
/// 存储器
/// </summary>
/// <typeparam name="TEntity">对象类型</typeparam>
/// <typeparam name="TKey">对象标识类型</typeparam>
public abstract class StoreBase<TEntity, TKey> : QueryStoreBase<TEntity, TKey>, IStore<TEntity, TKey> where TEntity : class, IKey<TKey>
{
    /// <summary>
    /// 获取当前工作单元提供的实体审计字段设置器。
    /// </summary>
    protected virtual IAuditPropertySetter AuditPropertySetter =>
        UnitOfWork.ServiceProvider?.GetService(typeof(IAuditPropertySetter)) as IAuditPropertySetter;

    /// <summary>
    /// 初始化一个 <see cref="StoreBase{TEntity, TKey}" /> 类型的实例。
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    protected StoreBase(IUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
        IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
        ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
        metadataOptions, typeConverterResolver)
    {
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体为空时抛出。</exception>
    public virtual void Add(TEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        RegisterDomainEventSource(entity);
        AuditPropertySetter?.SetCreationProperties(entity);
        AuditPropertySetter?.SetModificationProperties(entity);
        Set.Add(entity);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体集合为空时抛出。</exception>
    public virtual void Add(IEnumerable<TEntity> entities)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        var items = entities.ToList();
        foreach (var entity in items)
        {
            if (entity == null)
                throw new ArgumentException("实体集合不能包含空值。", nameof(entities));
            RegisterDomainEventSource(entity);
            AuditPropertySetter?.SetCreationProperties(entity);
            AuditPropertySetter?.SetModificationProperties(entity);
        }
        Set.AddRange(items);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体为空时抛出。</exception>
    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        RegisterDomainEventSource(entity);
        AuditPropertySetter?.SetCreationProperties(entity);
        AuditPropertySetter?.SetModificationProperties(entity);
        await Set.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体集合为空时抛出。</exception>
    public virtual async Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        var items = entities.ToList();
        foreach (var entity in items)
        {
            if (entity == null)
                throw new ArgumentException("实体集合不能包含空值。", nameof(entities));
            RegisterDomainEventSource(entity);
            AuditPropertySetter?.SetCreationProperties(entity);
            AuditPropertySetter?.SetModificationProperties(entity);
        }
        await Set.AddRangeAsync(items, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体为空时抛出。</exception>
    public virtual void Update(TEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        RegisterDomainEventSource(entity);
        AuditPropertySetter?.SetModificationProperties(entity);
        Set.Update(entity);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体集合为空时抛出。</exception>
    public virtual void Update(IEnumerable<TEntity> entities)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        var items = entities.ToList();
        foreach (var entity in items)
        {
            if (entity == null)
                throw new ArgumentException("实体集合不能包含空值。", nameof(entities));
            RegisterDomainEventSource(entity);
            AuditPropertySetter?.SetModificationProperties(entity);
        }
        Set.UpdateRange(items);
    }

    /// <inheritdoc />
    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体集合为空时抛出。</exception>
    public virtual async Task UpdateAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        var items = entities.ToList();
        foreach (var entity in items)
        {
            if (entity == null)
                throw new ArgumentException("实体集合不能包含空值。", nameof(entities));
            RegisterDomainEventSource(entity);
            AuditPropertySetter?.SetModificationProperties(entity);
        }
        await Set.UpdateRangeAsync(items, cancellationToken);
    }

    /// <inheritdoc />
    public virtual void Remove(object id)
    {
        var entity = Find(id);
        Delete(entity);
    }

    /// <summary>
    /// 删除实体。
    /// </summary>
    /// <param name="entity">实体</param>
    private void Delete(TEntity entity)
    {
        if (entity == null)
            return;
        RegisterDomainEventSource(entity);
        AuditPropertySetter?.SetDeletionProperties(entity);
        AuditPropertySetter?.SetModificationProperties(entity);
        if (entity is ISoftDelete model)
        {
            model.IsDeleted = true;
            Set.Update(entity);
            return;
        }

        Set.Remove(entity);
    }

    /// <inheritdoc />
    public virtual void Remove(TEntity entity)
    {
        if (entity == null)
            return;
        RegisterDomainEventSource(entity);
        Remove(entity.Id);
    }

    /// <inheritdoc />
    public virtual void Remove(IEnumerable<TKey> ids)
    {
        if (ids == null)
            return;
        var list = FindByIds(ids);
        Delete(list);
    }

    /// <summary>
    /// 删除实体。
    /// </summary>
    /// <param name="list">实体集合</param>
    private void Delete(List<TEntity> list)
    {
        if (list == null)
            return;
        if (!list.Any())
            return;
        foreach (var entity in list)
        {
            RegisterDomainEventSource(entity);
            AuditPropertySetter?.SetDeletionProperties(entity);
            AuditPropertySetter?.SetModificationProperties(entity);
        }
        if (list[0] is ISoftDelete)
        {
            foreach (var entity in list.Select(t => (ISoftDelete)t))
                entity.IsDeleted = true;
            Set.UpdateRange(list);
            return;
        }
        Set.RemoveRange(list);
    }

    /// <inheritdoc />
    public virtual void Remove(IEnumerable<TEntity> entities)
    {
        if (entities == null)
            return;
        var items = entities.ToList();
        foreach (var entity in items)
        {
            if (entity != null)
                RegisterDomainEventSource(entity);
        }
        Remove(items.Select(t => t.Id));
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await FindAsync(id, cancellationToken);
        Delete(entity);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entity == null)
            return;
        RegisterDomainEventSource(entity);
        await RemoveAsync(entity.Id, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ids == null)
            return;
        var entities = await FindByIdsAsync(ids, cancellationToken);
        Delete(entities);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entities == null)
            return;
        var items = entities.ToList();
        foreach (var entity in items)
        {
            if (entity != null)
                RegisterDomainEventSource(entity);
        }
        await RemoveAsync(items.Select(t => t.Id), cancellationToken);
    }

    /// <summary>
    /// 登记由当前存储器写入的领域事件源。
    /// </summary>
    /// <param name="entity">当前写入实体。</param>
    private void RegisterDomainEventSource(TEntity entity)
    {
        if (entity is IHasDomainEvents source)
            UnitOfWork.RegisterDomainEventSource(source);
    }
}
