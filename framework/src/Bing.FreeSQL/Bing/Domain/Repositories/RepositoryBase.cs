using Bing.Data.Stores;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Domain.Entities;
using IUnitOfWork = Bing.Uow.IUnitOfWork;

namespace Bing.Domain.Repositories;

/// <summary>
/// 仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public abstract class RepositoryBase<TEntity> : RepositoryBase<TEntity, Guid>, IRepository<TEntity>
    where TEntity : class, IAggregateRoot<TEntity, Guid>
{
    /// <summary>
    /// 初始化一个<see cref="RepositoryBase{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    protected RepositoryBase(IUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
        IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
        ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
        metadataOptions, typeConverterResolver)
    {
    }
}

/// <summary>
/// 仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">实体标识类型</typeparam>
public abstract class RepositoryBase<TEntity, TKey> : StoreBase<TEntity, TKey>, IRepository<TEntity, TKey>
    where TEntity : class, IAggregateRoot<TEntity, TKey>
{
    /// <summary>
    /// 初始化一个<see cref="RepositoryBase{TEntity,TKey}"/>类型的实例
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    protected RepositoryBase(IUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
        IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
        ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
        metadataOptions, typeConverterResolver)
    {
    }

    /// <summary>
    /// 获取工作单元
    /// </summary>
    /// <returns>当前仓储使用的工作单元。</returns>
    public IUnitOfWork GetUnitOfWork() => UnitOfWork;
}