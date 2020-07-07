using System;
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
        /// 创建获取属性表达式
        /// </summary>
        /// <param name="propertyInfo">属性信息</param>
        public static MemberExpression CreateGetPropertyExpression(this PropertyInfo propertyInfo) => propertyInfo.CreateGetPropertyExpression(propertyInfo.DeclaringType.CreateParameterExpression());

        /// <summary>
        /// 创建参数表达式
        /// </summary>
        /// <param name="type">类型</param>
        private static ParameterExpression CreateParameterExpression(this Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));
            return Expression.Parameter(type, "o");
        }

        /// <summary>
        /// 创建获取属性表达式
        /// </summary>
        /// <param name="propertyInfo">属性信息</param>
        /// <param name="parameterExpression">参数表达式</param>
        public static MemberExpression CreateGetPropertyExpression(this PropertyInfo propertyInfo,
            ParameterExpression parameterExpression)
        {
            if (propertyInfo is null)
                throw new ArgumentNullException(nameof(propertyInfo));
            if (parameterExpression is null)
                throw new ArgumentNullException(nameof(parameterExpression));
            if (!propertyInfo.DeclaringType.GetTypeInfo().IsAssignableFrom(parameterExpression.Type.GetTypeInfo()))
                throw new InvalidOperationException(
                    $"The type of {nameof(parameterExpression)} ({parameterExpression.Type}) is not assignable to the type of {nameof(propertyInfo.DeclaringType)} ({propertyInfo.DeclaringType}) passed in the {nameof(propertyInfo)} parameter.");
            return Expression.Property(parameterExpression, propertyInfo);
        }

        /// <summary>
        /// 创建获取属性Lambda表达式
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="propertyInfo">属性信息</param>
        public static Expression<Func<T, TProperty>> CreateGetPropertyLambdaExpression<T, TProperty>(this PropertyInfo propertyInfo) =>
            propertyInfo.CreateGetPropertyLambdaExpression<T, TProperty>(typeof(T).CreateParameterExpression());

        /// <summary>
        /// 创建获取属性Lambda表达式
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="propertyInfo">属性信息</param>
        /// <param name="parameterExpression">参数表达式</param>
        public static Expression<Func<T, TProperty>> CreateGetPropertyLambdaExpression<T, TProperty>(this PropertyInfo propertyInfo, ParameterExpression parameterExpression)
        {
            if (propertyInfo is null)
                throw new ArgumentNullException(nameof(propertyInfo));
            if (parameterExpression is null)
                throw new ArgumentNullException(nameof(parameterExpression));
            var propertyExpression = propertyInfo.CreateGetPropertyExpression(parameterExpression);
            Expression expression = propertyExpression;
            var type = typeof(TProperty);
            if (propertyInfo.PropertyType != type)
                expression = Expression.Convert(expression, type);
            return Expression.Lambda<Func<T, TProperty>>(expression, propertyExpression.Expression as ParameterExpression);
        }
    }
}
