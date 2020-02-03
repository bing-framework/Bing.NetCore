using System;
using System.Linq.Expressions;
using System.Reflection;
using Bing.Extensions;

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
        /// <returns></returns>
        public static PropertyInfo ExtractPropertyInfo(this LambdaExpression expression)
        {
            return expression.ExtractMemberInfo() as PropertyInfo;
        }

        /// <summary>
        /// 提取字段信息
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <returns></returns>
        public static FieldInfo ExtractFieldInfo(this LambdaExpression expression)
        {
            return expression.ExtractMemberInfo() as FieldInfo;
        }

        /// <summary>
        /// 提取成员信息
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <returns></returns>
        public static MemberInfo ExtractMemberInfo(this LambdaExpression expression)
        {
            expression.CheckNotNull(nameof(expression));

            MemberInfo info;
            try
            {
                MemberExpression operand;
                LambdaExpression lambda = expression;
                if (lambda.Body is UnaryExpression body)
                {
                    operand=body.Operand as MemberExpression;
                }
                else
                {
                    operand = lambda.Body as MemberExpression;
                }

                MemberInfo member = operand.Member;
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
