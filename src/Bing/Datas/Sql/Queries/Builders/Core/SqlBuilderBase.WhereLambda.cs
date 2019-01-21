using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Datas.Queries;
using Bing.Utils;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // 设置Lambda查询条件
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 设置相等查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Equal(string column, object value)
        {
            return Where(column, value);
        }

        /// <summary>
        /// 设置相等查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Equal<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value);
        }

        /// <summary>
        /// 设置不相等查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder NotEqual(string column, object value)
        {
            return Where(column, value, Operator.NotEqual);
        }

        /// <summary>
        /// 设置不相等查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder NotEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value, Operator.NotEqual);
        }

        /// <summary>
        /// 设置大于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Greater(string column, object value)
        {
            return Where(column, value, Operator.Greater);
        }

        /// <summary>
        /// 设置大于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Greater<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value, Operator.Greater);
        }

        /// <summary>
        /// 设置大于等于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder GreaterEqual(string column, object value)
        {
            return Where(column, value, Operator.GreaterEqual);
        }

        /// <summary>
        /// 设置大于等于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder GreaterEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value, Operator.GreaterEqual);
        }

        /// <summary>
        /// 设置小于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Less(string column, object value)
        {
            return Where(column, value, Operator.Less);
        }

        /// <summary>
        /// 设置小于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Less<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value, Operator.Less);
        }

        /// <summary>
        /// 设置小于等于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder LessEqual(string column, object value)
        {
            return Where(column, value, Operator.LessEqual);
        }

        /// <summary>
        /// 设置小于等于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder LessEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value, Operator.LessEqual);
        }

        /// <summary>
        /// 设置模糊匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Contains(string column, object value)
        {
            return Where(column, value, Operator.Contains);
        }

        /// <summary>
        /// 设置模糊匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Contains<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value, Operator.Contains);
        }

        /// <summary>
        /// 设置头匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Starts(string column, object value)
        {
            return Where(column, value, Operator.Starts);
        }

        /// <summary>
        /// 设置头匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Starts<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value, Operator.Starts);
        }

        /// <summary>
        /// 设置尾匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Ends(string column, object value)
        {
            return Where(column, value, Operator.Ends);
        }

        /// <summary>
        /// 设置尾匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISqlBuilder Ends<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class
        {
            return Where(expression, value, Operator.Ends);
        }

        /// <summary>
        /// 设置Is Null查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        public ISqlBuilder IsNull(string column)
        {
            WhereClause.IsNull(column);
            return this;
        }

        /// <summary>
        /// 设置Is Null查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <returns></returns>
        public ISqlBuilder IsNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class
        {
            WhereClause.IsNull(expression);
            return this;
        }

        /// <summary>
        /// 设置Is Not Null查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        public ISqlBuilder IsNotNull(string column)
        {
            WhereClause.IsNotNull(column);
            return this;
        }

        /// <summary>
        /// 设置Is Not Null查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <returns></returns>
        public ISqlBuilder IsNotNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class
        {
            WhereClause.IsNotNull(expression);
            return this;
        }

        /// <summary>
        /// 设置空条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        public ISqlBuilder IsEmpty(string column)
        {
            WhereClause.IsEmpty(column);
            return this;
        }

        /// <summary>
        /// 设置空条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <returns></returns>
        public ISqlBuilder IsEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class
        {
            WhereClause.IsEmpty(expression);
            return this;
        }

        /// <summary>
        /// 设置非空条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        public ISqlBuilder IsNotEmpty(string column)
        {
            WhereClause.IsNotEmpty(column);
            return this;
        }

        /// <summary>
        /// 设置非空条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <returns></returns>
        public ISqlBuilder IsNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class
        {
            WhereClause.IsNotEmpty(expression);
            return this;
        }

        /// <summary>
        /// 设置In条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        public ISqlBuilder In(string column, IEnumerable<object> values)
        {
            WhereClause.In(column, values);
            return this;
        }

        /// <summary>
        /// 设置In条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        public ISqlBuilder In<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values) where TEntity : class
        {
            WhereClause.In(expression, values);
            return this;
        }

        /// <summary>
        /// 设置Not In条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        public ISqlBuilder NotIn(string column, IEnumerable<object> values)
        {
            WhereClause.NotIn(column, values);
            return this;
        }

        /// <summary>
        /// 设置Not In条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        public ISqlBuilder NotIn<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values) where TEntity : class
        {
            WhereClause.NotIn(expression, values);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, int? min, int? max, Boundary boundary = Boundary.Both) where TEntity : class
        {
            WhereClause.Between(expression, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, long? min, long? max, Boundary boundary = Boundary.Both) where TEntity : class
        {
            WhereClause.Between(expression, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, float? min, float? max, Boundary boundary = Boundary.Both) where TEntity : class
        {
            WhereClause.Between(expression, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, double? min, double? max, Boundary boundary = Boundary.Both) where TEntity : class
        {
            WhereClause.Between(expression, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, decimal? min, decimal? max, Boundary boundary = Boundary.Both) where TEntity : class
        {
            WhereClause.Between(expression, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="includeTime">是否包含时间</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, DateTime? min, DateTime? max, bool includeTime = true,
            Boundary? boundary = null) where TEntity : class
        {
            WhereClause.Between(expression, min, max, includeTime, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between(string column, int? min, int? max, Boundary boundary = Boundary.Both)
        {
            WhereClause.Between(column, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between(string column, long? min, long? max, Boundary boundary = Boundary.Both)
        {
            WhereClause.Between(column, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between(string column, float? min, float? max, Boundary boundary = Boundary.Both)
        {
            WhereClause.Between(column, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between(string column, double? min, double? max, Boundary boundary = Boundary.Both)
        {
            WhereClause.Between(column, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between(string column, decimal? min, decimal? max, Boundary boundary = Boundary.Both)
        {
            WhereClause.Between(column, min, max, boundary);
            return this;
        }

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="includeTime">是否包含时间</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        public ISqlBuilder Between(string column, DateTime? min, DateTime? max, bool includeTime = true,
            Boundary? boundary = null)
        {
            WhereClause.Between(column, min, max, includeTime, boundary);
            return this;
        }
    }
}
