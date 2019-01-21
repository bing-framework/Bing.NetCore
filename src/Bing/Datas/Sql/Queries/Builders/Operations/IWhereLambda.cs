using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Datas.Queries;

namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 设置Lambda查询条件
    /// </summary>
    public interface IWhereLambda
    {
        /// <summary>
        /// 设置相等查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Equal(string column, object value);

        /// <summary>
        /// 设置相等查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Equal<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置不相等查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder NotEqual(string column, object value);

        /// <summary>
        /// 设置不相等查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder NotEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置大于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Greater(string column, object value);

        /// <summary>
        /// 设置大于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Greater<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置大于等于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder GreaterEqual(string column, object value);

        /// <summary>
        /// 设置大于等于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder GreaterEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置小于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Less(string column, object value);

        /// <summary>
        /// 设置小于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Less<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置小于等于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder LessEqual(string column, object value);

        /// <summary>
        /// 设置小于等于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder LessEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置模糊匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Contains(string column, object value);

        /// <summary>
        /// 设置模糊匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Contains<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置头匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Starts(string column, object value);

        /// <summary>
        /// 设置头匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Starts<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置尾匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Ends(string column, object value);

        /// <summary>
        /// 设置尾匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Ends<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置Is Null查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlBuilder IsNull(string column);

        /// <summary>
        /// 设置Is Null查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlBuilder IsNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置Is Not Null查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlBuilder IsNotNull(string column);

        /// <summary>
        /// 设置Is Not Null查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlBuilder IsNotNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置空条件，范例：[Name] Is Null Or [Name]=''
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlBuilder IsEmpty(string column);

        /// <summary>
        /// 设置空条件，范例：[Name] Is Null Or [Name]=''
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlBuilder IsEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置非空条件，范例：[Name] Is Null Or [Name]&lt;&gt;''
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlBuilder IsNotEmpty(string column);

        /// <summary>
        /// 设置非空条件，范例：[Name] Is Null Or [Name]&lt;&gt;''
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlBuilder IsNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置In条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlBuilder In(string column, IEnumerable<object> values);

        /// <summary>
        /// 设置In条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlBuilder In<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values) where TEntity : class;

        /// <summary>
        /// 设置Not In条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlBuilder NotIn(string column, IEnumerable<object> values);

        /// <summary>
        /// 设置Not In条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlBuilder NotIn<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, int? min, int? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, long? min, long? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, float? min, float? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, double? min, double? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, decimal? min, decimal? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="includeTime">是否包含时间</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, DateTime? min, DateTime? max,
            bool includeTime = true, Boundary? boundary = null) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, int? min, int? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, long? min, long? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, float? min, float? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, double? min, double? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, decimal? min, decimal? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="includeTime">是否包含时间</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, DateTime? min, DateTime? max, bool includeTime = true, Boundary? boundary = null);
    }
}
