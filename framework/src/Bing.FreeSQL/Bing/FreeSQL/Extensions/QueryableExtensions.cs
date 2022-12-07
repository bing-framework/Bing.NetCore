﻿using System;
using System.Linq;
using System.Threading.Tasks;
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
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static async Task<IQueryable<TEntity>> PageAsync<TEntity>(this IQueryable<TEntity> query, IPager pager) where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        Helper.InitOrder(query, pager);
        var select = query.RestoreToSelect();
        if (pager.TotalCount <= 0)
            pager.TotalCount = (int)await select.CountAsync();
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
    /// <exception cref="ArgumentNullException"></exception>
    public static async Task<PagerList<TEntity>> ToPagerListAsync<TEntity>(this IQueryable<TEntity> query, IPager pager) where TEntity : class
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        query = await query.PageAsync(pager);
        var select = query.RestoreToSelect();
        return new PagerList<TEntity>(pager, await select.ToListAsync());
    }

    #endregion
}