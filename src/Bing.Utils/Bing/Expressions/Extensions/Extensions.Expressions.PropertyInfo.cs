using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Bing.Expressions
{
    /// <summary>
    /// 表达式(<see cref="Expression"/>) 扩展
    /// </summary>
    public static partial class ExpressionExtensions
    {
        /// <summary>
        /// 获取属性信息
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="expression">表达式</param>
        public static PropertyInfo GetPropertyInfo<T, TProperty>(this Expression<Func<T, TProperty>> expression)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));
            var member = expression.Body as MemberExpression;

            ArgumentException CreateExpressionNotPropertyException()=>new ArgumentException($"The expression parameter ({nameof(expression)}) is not a property expression.");

            if (member is null && expression.Body.NodeType == ExpressionType.Convert)
            {
                var operand = (expression.Body as UnaryExpression)?.Operand;
                if (operand != null)
                    member = operand as MemberExpression;
            }

            if (member is null)
                throw CreateExpressionNotPropertyException();
            if (!(member.Member is PropertyInfo propertyInfo))
                throw CreateExpressionNotPropertyException();
            return propertyInfo;
        }

        /// <summary>
        /// 获取属性信息
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="expression">表达式</param>
        public static PropertyInfo GetPropertyInfo<T, TProperty>(this T source, Expression<Func<T, TProperty>> expression) => expression.GetPropertyInfo();

        /// <summary>
        /// 获取属性信息集合
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="expressions">表达式集合</param>
        public static IEnumerable<PropertyInfo> GetPropertyInfos<T>(this T source, params Expression<Func<T, object>>[] expressions) =>
            source.GetPropertyInfos((IEnumerable<Expression<Func<T, object>>>)expressions);

        /// <summary>
        /// 获取属性信息集合
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="expressions">表达式集合</param>
        public static IEnumerable<PropertyInfo> GetPropertyInfos<T>(this T source, IEnumerable<Expression<Func<T, object>>> expressions)
        {
            if(source is null)
                throw new ArgumentNullException(nameof(source));
            if(expressions is null)
                throw new ArgumentNullException(nameof(expressions));
            return expressions.GetPropertyInfos();
        }

        /// <summary>
        /// 获取属性信息集合
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="expressions">表达式集合</param>
        public static IEnumerable<PropertyInfo> GetPropertyInfos<TSource>(this IEnumerable<Expression<Func<TSource, object>>> expressions) =>
            expressions.Select(e => e.GetPropertyInfo());
    }
}
