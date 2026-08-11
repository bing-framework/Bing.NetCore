using Bing.Extensions;
using Bing.Trees;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using IUnitOfWork = Bing.Uow.IUnitOfWork;

namespace Bing.Domain.Repositories;

/// <summary>
/// 树型仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public abstract class TreeRepositoryBase<TEntity> : TreeRepositoryBase<TEntity, Guid, Guid?>, ITreeRepository<TEntity>
    where TEntity : class, ITreeEntity<TEntity, Guid, Guid?>
{
    /// <summary>
    /// 初始化一个<see cref="TreeRepositoryBase{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    protected TreeRepositoryBase(IUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
        IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
        ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
        metadataOptions, typeConverterResolver)
    {
    }

    /// <summary>
    /// 生成排序号
    /// </summary>
    /// <param name="parentId">父标识</param>
    public override async Task<int> GenerateSortIdAsync(Guid? parentId)
    {
        var maxSortId = await Find(x => x.ParentId == parentId).RestoreToSelect().MaxAsync(x => x.SortId);
        return maxSortId.SafeValue() + 1;
    }
}

/// <summary>
/// 树型仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">实体标识类型</typeparam>
/// <typeparam name="TParentId">父标识类型</typeparam>
public abstract class TreeRepositoryBase<TEntity, TKey, TParentId> : RepositoryBase<TEntity, TKey>, ITreeRepository<TEntity, TKey, TParentId>
    where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>
{
    /// <summary>
    /// 初始化一个<see cref="TreeRepositoryBase{TEntity,TKey,TParentId}"/>类型的实例
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    protected TreeRepositoryBase(IUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
        IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
        ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
        metadataOptions, typeConverterResolver)
    {
    }

    /// <summary>
    /// 生成排序号
    /// </summary>
    /// <param name="parentId">父标识</param>
    public abstract Task<int> GenerateSortIdAsync(TParentId parentId);

    /// <summary>
    /// 获取全部下级实体
    /// </summary>
    /// <param name="parent">父实体</param>
    public virtual async Task<List<TEntity>> GetAllChildrenAsync(TEntity parent)
    {
        var list = await FindAllAsync(t => t.Path.StartsWith(parent.Path));
        return list.Where(t => !t.Id.Equals(parent.Id)).ToList();
    }
}