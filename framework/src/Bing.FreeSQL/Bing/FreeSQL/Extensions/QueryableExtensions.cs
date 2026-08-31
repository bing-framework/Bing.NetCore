using Bing.Data;
using Bing.Data.Queries.Internal;

namespace Bing.FreeSQL.Extensions;

/// <summary>
/// <see cref="IQueryable{T}"/> 扩展
/// </summary>
public static partial class QueryableExtensions
{
    #region Page(分页，包含排序)

    /// <summary>
    /// 分页，包含排序
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="pager">分页对象</param>
    /// <returns>包含排序和分页条件的查询对象异步任务。</returns>
    /// <exception cref="ArgumentNullException">数据源或分页对象为空时抛出。</exception>
    /// <exception cref="ArgumentException">未设置排序字段时抛出。</exception>
    public static Task<IQueryable<TEntity>> PageAsync<TEntity>(this IQueryable<TEntity> query, IPager pager)
        where TEntity : class => PageAsync(query, pager, CancellationToken.None);

    /// <summary>
    /// 分页，包含排序，并将取消令牌传递给总数查询。
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="pager">分页对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已应用排序和分页条件的数据源。</returns>
    /// <exception cref="ArgumentNullException">当数据源或分页对象为空时抛出。</exception>
    /// <exception cref="ArgumentException">当未设置排序字段时抛出。</exception>
    public static async Task<IQueryable<TEntity>> PageAsync<TEntity>(this IQueryable<TEntity> query, IPager pager,
        CancellationToken cancellationToken) where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        cancellationToken.ThrowIfCancellationRequested();
        Helper.InitOrder(query, pager);
        var select = query.RestoreToSelect();
        if (pager.TotalCount <= 0)
            pager.TotalCount = (int)await select.CountAsync(cancellationToken);
        var orderedQueryable = Helper.GetOrderedQueryable(query, pager);
        if (orderedQueryable == null)
            throw new ArgumentException("必须设置排序字段");
        return orderedQueryable.Skip(pager.GetSkipCount()).Take(pager.PageSize);
    }

    #endregion

    #region ToPagerListAsync(转换为分页列表)

    /// <summary>
    /// 转换为分页列表，包含排序分页操作
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="pager">分页对象</param>
    /// <returns>包含分页结果的异步任务。</returns>
    /// <exception cref="ArgumentNullException">数据源或分页对象为空时抛出。</exception>
    public static Task<PagerList<TEntity>> ToPagerListAsync<TEntity>(this IQueryable<TEntity> query, IPager pager)
        where TEntity : class => ToPagerListAsync(query, pager, CancellationToken.None);

    /// <summary>
    /// 转换为分页列表，包含排序分页操作，并将取消令牌传递给总数和数据查询。
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">数据源</param>
    /// <param name="pager">分页对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果。</returns>
    /// <exception cref="ArgumentNullException">当数据源或分页对象为空时抛出。</exception>
    public static async Task<PagerList<TEntity>> ToPagerListAsync<TEntity>(this IQueryable<TEntity> query, IPager pager,
        CancellationToken cancellationToken) where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        cancellationToken.ThrowIfCancellationRequested();
        query = await query.PageAsync(pager, cancellationToken);
        var select = query.RestoreToSelect();
        return new PagerList<TEntity>(pager, await select.ToListAsync(cancellationToken));
    }

    #endregion
}