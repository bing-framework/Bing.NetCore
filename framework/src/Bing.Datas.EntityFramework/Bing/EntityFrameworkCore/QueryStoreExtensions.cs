using System.Linq.Dynamic.Core;
using Bing.Data;
using Bing.Data.Queries;
using Bing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bing.EntityFrameworkCore;

/// <summary>
/// 查询存储器(<see cref="IQueryStore{TEntity, TKey}"/>) 扩展
/// </summary>
public static class QueryStoreExtensions
{
    /// <summary>
    /// 查询
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="store">查询存储器</param>
    /// <param name="query">查询对象</param>
    public static List<TEntity> Query<TEntity, TKey>(this IQueryStore<TEntity, TKey> store, IQueryBase<TEntity> query)
        where TEntity : class, IKey<TKey>
    {
        return Query<TEntity, TKey>(store.Find(), query).ToList();
    }

    /// <summary>
    /// 查询 - 返回未跟踪的实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="store">查询存储器</param>
    /// <param name="query">查询对象</param>
    public static List<TEntity> QueryAsNoTracking<TEntity, TKey>(this IQueryStore<TEntity, TKey> store, IQueryBase<TEntity> query)
        where TEntity : class, IKey<TKey>
    {
        return Query<TEntity, TKey>(store.FindAsNoTracking(), query).ToList();
    }

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="store">查询存储器</param>
    /// <param name="query">查询对象</param>
    public static PagerList<TEntity> PagerQuery<TEntity, TKey>(this IQueryStore<TEntity, TKey> store, IQueryBase<TEntity> query)
        where TEntity : class, IKey<TKey>
    {
        return store.Find().Where(query).ToPagerList(query.GetPager());
    }

    /// <summary>
    /// 分页查询 - 返回未跟踪的实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="store">查询存储器</param>
    /// <param name="query">查询对象</param>
    public static PagerList<TEntity> PagerQueryAsNoTracking<TEntity, TKey>(this IQueryStore<TEntity, TKey> store, IQueryBase<TEntity> query)
        where TEntity : class, IKey<TKey>
    {
        return store.FindAsNoTracking().Where(query).ToPagerList(query.GetPager());
    }

    /// <summary>
    /// 查询
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="store">查询存储器</param>
    /// <param name="query">查询对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<List<TEntity>> QueryAsync<TEntity, TKey>(this IQueryStore<TEntity, TKey> store, IQueryBase<TEntity> query, CancellationToken cancellationToken = default)
        where TEntity : class, IKey<TKey>
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Query<TEntity, TKey>(store.Find(), query).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查询 - 返回未跟踪的实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="store">查询存储器</param>
    /// <param name="query">查询对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<List<TEntity>> QueryAsNoTrackingAsync<TEntity, TKey>(this IQueryStore<TEntity, TKey> store, IQueryBase<TEntity> query, CancellationToken cancellationToken = default)
        where TEntity : class, IKey<TKey>
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Query<TEntity, TKey>(store.FindAsNoTracking(), query).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="store">查询存储器</param>
    /// <param name="query">查询对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<PagerList<TEntity>> PagerQueryAsync<TEntity, TKey>(this IQueryStore<TEntity, TKey> store, IQueryBase<TEntity> query, CancellationToken cancellationToken = default)
        where TEntity : class, IKey<TKey>
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await store.Find().Where(query).ToPagerListAsync(query.GetPager(), cancellationToken);
    }

    /// <summary>
    /// 分页查询 - 返回未跟踪的实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="store">查询存储器</param>
    /// <param name="query">查询对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<PagerList<TEntity>> PagerQueryAsNoTrackingAsync<TEntity, TKey>(this IQueryStore<TEntity, TKey> store, IQueryBase<TEntity> query, CancellationToken cancellationToken = default)
        where TEntity : class, IKey<TKey>
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await store.FindAsNoTracking().Where(query).ToPagerListAsync(query.GetPager(), cancellationToken);
    }

    /// <summary>
    /// 获取查询结果
    /// </summary>
    /// <param name="queryable">数据源</param>
    /// <param name="query">查询对象</param>
    private static IQueryable<TEntity> Query<TEntity, TKey>(IQueryable<TEntity> queryable, IQueryBase<TEntity> query)
        where TEntity : class, IKey<TKey>
    {
        queryable = queryable.Where(query);
        var order = query.GetOrder();
        return string.IsNullOrWhiteSpace(order) ? queryable : queryable.OrderBy(order);
    }
}
