using System;
using System.Linq.Expressions;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// Lambda表达式(<see cref="LambdaExpression"/>) 扩展
    /// </summary>
    public static class LambdaExpressionExtensions
    {
        /// <summary>
        /// 提取属性信息
        /// </summary>
        /// <param name="expression">表达式</param>
        public static PropertyInfo ExtractPropertyInfo(this LambdaExpression expression) => expression.ExtractMemberInfo() as PropertyInfo;

        /// <summary>
        /// 提取字段信息
        /// </summary>
        /// <param name="expression">表达式</param>
        public static FieldInfo ExtractFieldInfo(this LambdaExpression expression) => expression.ExtractMemberInfo() as FieldInfo;

        /// <summary>
        /// 提取成员信息
        /// </summary>
        /// <param name="expression">表达式</param>
        public static MemberInfo ExtractMemberInfo(this LambdaExpression expression)
        {
            expression.CheckNotNull(nameof(expression));

            MemberInfo info;
            try
            {
                var lambda = expression;
                var operand = lambda.Body is UnaryExpression body
                    ? body.Operand as MemberExpression
                    : lambda.Body as MemberExpression;

                var member = operand.Member;
                info = member;
            }
            catch (Exception e)
            {
                throw new ArgumentNullException("属性或字段访问器表达式不是 “ o => o.Property” 格式",e);
            }

            return info;
        }
    }
}
