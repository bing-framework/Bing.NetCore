using System;
using System.Linq;
using System.Threading.Tasks;
using Bing.Domains.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.Extensions
{
    /// <summary>
    /// <see cref="IQueryable{T}"/> 扩展
    /// </summary>
    public static partial class QueryableExtensions
    {

        #region ToPagerListAsync(转换为分页列表)

        /// <summary>
        /// 转换为分页列表，包含排序分页操作
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="pager">分页对象</param>
        /// <returns></returns>
        public static async Task<PagerList<TEntity>> ToPagerListAsync<TEntity>(this IQueryable<TEntity> query,
            IPager pager)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (pager == null)
            {
                throw new ArgumentNullException(nameof(pager));
            }
            return new PagerList<TEntity>(pager,await query.Page(pager).ToListAsync());
        }

        #endregion
    }
}
