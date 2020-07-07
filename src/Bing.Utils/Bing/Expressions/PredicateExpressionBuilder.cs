using System;
using System.Linq.Expressions;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Expressions
{
    /// <summary>
    /// 谓词表达式生成器
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public class PredicateExpressionBuilder<TEntity>
    {
        /// <summary>
        /// 参数表达式
        /// </summary>
        private readonly ParameterExpression _parameter;

        /// <summary>
        /// 结果表达式
        /// </summary>
        private Expression _result;

        /// <summary>
        /// 初始化一个<see cref="PredicateExpressionBuilder{TEntity}"/>类型的实例
        /// </summary>
        public PredicateExpressionBuilder() => _parameter = Expression.Parameter(typeof(TEntity), "t");

        /// <summary>
        /// 获取参数表达式
        /// </summary>
        public ParameterExpression GetParameter() => _parameter;

        /// <summary>
        /// 添加表达式
        /// </summary>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="propertyExpression">属性表达式</param>
        /// <param name="operator">运算符</param>
        /// <param name="value">值</param>
        public void Append<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, Operator @operator, object value) =>
            _result = _result.And(_parameter.Property(Lambdas.GetMember(propertyExpression))
                .Operation(@operator, value));

        /// <summary>
        /// 添加表达式
        /// </summary>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="propertyExpression">属性表达式</param>
        /// <param name="operator">运算符</param>
        /// <param name="value">值</param>
        public void Append<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, Operator @operator,
            Expression value) =>
            _result = _result.And(_parameter.Property(Lambdas.GetMember(propertyExpression))
                .Operation(@operator, value));

        /// <summary>
        /// 添加表达式
        /// </summary>
        /// <param name="property">属性名</param>
        /// <param name="operator">运算符</param>
        /// <param name="value">值</param>
        public void Append(string property, Operator @operator, object value) => _result = _result.And(_parameter.Property(property).Operation(@operator, value));

        /// <summary>
        /// 添加表达式
        /// </summary>
        /// <param name="property">属性名</param>
        /// <param name="operator">运算符</param>
        /// <param name="value">值</param>
        public void Append(string property, Operator @operator, Expression value) => _result = _result.And(_parameter.Property(property).Operation(@operator, value));

        /// <summary>
        /// 清空
        /// </summary>
        public void Clear() => _result = null;

        /// <summary>
        /// 转换为Lambda表达式
        /// </summary>
        public Expression<Func<TEntity, bool>> ToLambda() => _result.ToLambda<Func<TEntity, bool>>(_parameter);
    }
}
