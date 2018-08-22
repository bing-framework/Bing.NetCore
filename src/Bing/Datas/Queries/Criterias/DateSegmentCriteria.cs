using System;
using System.Linq.Expressions;
using Bing.Utils.Extensions;

namespace Bing.Datas.Queries.Criterias
{
    /// <summary>
    /// 日期范围过滤条件 - 不包含时间
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    public class DateSegmentCriteria<TEntity, TProperty> : SegmentCriteriaBase<TEntity, TProperty, DateTime> where TEntity : class
    {
        /// <summary>
        /// 初始化一个<see cref="DateSegmentCriteria{TEntity,TProperty}"/>类型的实例
        /// </summary>
        /// <param name="propertyExpression">属性表达式</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        public DateSegmentCriteria(Expression<Func<TEntity, TProperty>> propertyExpression, DateTime? min,
            DateTime? max, Boundary boundary = Boundary.Both) : base(propertyExpression, min, max, boundary)
        {
        }

        /// <summary>
        /// 最小值是否大于最大值
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        protected override bool IsMinGreaterMax(DateTime? min, DateTime? max)
        {
            return min > max;
        }

        /// <summary>
        /// 获取最小值
        /// </summary>
        /// <returns></returns>
        protected override DateTime? GetMinValue()
        {
            return base.GetMinValue().SafeValue().Date;
        }

        /// <summary>
        /// 获取最大值
        /// </summary>
        /// <returns></returns>
        protected override DateTime? GetMaxValue()
        {
            return base.GetMaxValue().SafeValue().Date.AddDays(1);
        }
    }
}
