using System;
using System.Linq;
using System.Linq.Expressions;
using Bing.Datas.Queries;
using Bing.Datas.Queries.Criterias;
using Bing.Datas.Queries.Internal;
using Bing.Domains.Repositories;
using System.Linq.Dynamic.Core;
using Bing.Utils.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing
{
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
        /// <returns></returns>
        public static IQueryable<TEntity> Page<TEntity>(this IQueryable<TEntity> query, IPager pager)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (pager == null)
            {
                throw new ArgumentNullException(nameof(pager));
            }
            InitOrder(query, pager);            
            if (pager.TotalCount <= 0)
            {
                pager.TotalCount = query.Count();
            }
            var orderedQueryable = GetOrderedQueryable(query, pager);
            if (orderedQueryable == null)
            {
                throw new ArgumentException("必须设置排序字段");
            }
            return orderedQueryable.Skip(pager.GetSkipCount()).Take(pager.PageSize);
        }

        /// <summary>
        /// 初始化排序
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="pager">分页对象</param>
        private static void InitOrder<TEntity>(this IQueryable<TEntity> query, IPager pager)
        {
            if (string.IsNullOrWhiteSpace(pager.Order) == false)
            {
                return;
            }
            if (query.Expression.SafeString().Contains(".OrderBy("))
            {
                return;
            }
            pager.Order = "Id";
        }

        /// <summary>
        /// 获取排序查询对象
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="pager">分页对象</param>
        /// <returns></returns>
        private static IOrderedQueryable<TEntity> GetOrderedQueryable<TEntity>(this IQueryable<TEntity> query,
            IPager pager)
        {
            if (string.IsNullOrWhiteSpace(pager.Order))
            {
                return query as IOrderedQueryable<TEntity>;
            }
            return query.OrderBy(pager.Order);
        }

        #endregion

        #region ToPagerList(转换为分页列表)

        /// <summary>
        /// 转换为分页列表，包含排序分页操作
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="pager">分页对象</param>
        /// <returns></returns>
        public static PagerList<TEntity> ToPagerList<TEntity>(this IQueryable<TEntity> query, IPager pager)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (pager == null)
            {
                throw new ArgumentNullException(nameof(pager));
            }

            return new PagerList<TEntity>(pager, query.Page(pager).ToList());
        }

        #endregion        

        #region Where(添加查询条件)

        /// <summary>
        /// 添加查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="criteria">查询条件对象</param>
        /// <returns></returns>
        public static IQueryable<TEntity> Where<TEntity>(this IQueryable<TEntity> query, ICriteria<TEntity> criteria)
            where TEntity : class
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (criteria == null)
            {
                throw new ArgumentNullException(nameof(criteria));
            }

            var predicate = criteria.GetPredicate();
            if (predicate == null)
            {
                return query;
            }

            return query.Where(predicate);
        }

        #endregion

        #region WhereIf(添加查询条件)

        /// <summary>
        /// 添加查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="predicate">查询条件</param>
        /// <param name="condition">判断条件，该值为true时添加查询条件，否则忽略</param>
        /// <returns></returns>
        public static IQueryable<TEntity> WhereIf<TEntity>(this IQueryable<TEntity> query,
            Expression<Func<TEntity, bool>> predicate, bool condition)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return condition == false ? query : query.Where(predicate);
        }

        #endregion

        #region WhereIfNotEmpty(添加查询条件)

        /// <summary>
        /// 添加查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="predicate">查询条件，如果参数为空，则忽略该查询条件，范例：t => t.Name == ""，该查询条件被忽略。注意：一次仅能添加一个条件，范例：t => t.Name == "a" &amp;&amp; t.Mobile == "123"，不支持，将抛出异常</param>
        /// <returns></returns>
        public static IQueryable<TEntity> WhereIfNotEmpty<TEntity>(this IQueryable<TEntity> query,
            Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            predicate = Helper.GetWhereIfNotEmptyExpression(predicate);
            if (predicate == null)
            {
                return query;
            }
            return query.Where(predicate);
        }

        #endregion

        #region Between(添加范围查询条件)

        /// <summary>
        /// 添加范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public static IQueryable<TEntity> Between<TEntity, TProperty>(this IQueryable<TEntity> query,
            Expression<Func<TEntity, TProperty>> propertyExpression, int? min, int? max,
            Boundary boundary = Boundary.Both) where TEntity : class
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return query.Where(new IntSegmentCriteria<TEntity, TProperty>(propertyExpression, min, max, boundary));
        }

        /// <summary>
        /// 添加范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public static IQueryable<TEntity> Between<TEntity, TProperty>(this IQueryable<TEntity> query,
            Expression<Func<TEntity, TProperty>> propertyExpression, double? min, double? max,
            Boundary boundary = Boundary.Both) where TEntity : class
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return query.Where(new DoubleSegmentCriteria<TEntity, TProperty>(propertyExpression, min, max, boundary));
        }

        /// <summary>
        /// 添加范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="propertyExpression">属性表达式，范例：t => t.Age</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public static IQueryable<TEntity> Between<TEntity, TProperty>(this IQueryable<TEntity> query,
            Expression<Func<TEntity, TProperty>> propertyExpression, decimal? min, decimal? max,
            Boundary boundary = Boundary.Both) where TEntity : class
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return query.Where(new DecimalSegmentCriteria<TEntity, TProperty>(propertyExpression, min, max, boundary));
        }

        /// <summary>
        /// 添加范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="propertyExpression">属性表达式，范例：t => t.Time</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public static IQueryable<TEntity> Between<TEntity, TProperty>(this IQueryable<TEntity> query,
            Expression<Func<TEntity, TProperty>> propertyExpression, DateTime? min, DateTime? max,
            Boundary boundary = Boundary.Both) where TEntity : class
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return query.Where(new DateTimeSegmentCriteria<TEntity, TProperty>(propertyExpression, min, max, boundary));
        }

        #endregion
    }
}
