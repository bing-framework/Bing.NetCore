using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 对象(<see cref="object"/>) 扩展
    /// </summary>
    public static partial class ObjectExtensions
    {
        /// <summary>
        /// 表达式复制
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">数据源</param>
        public static T ExpressionCopy<T>(this T source) => Copier<T>.Copy(source);

        /// <summary>
        /// 复制器
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        private static class Copier<T>
        {
            /// <summary>
            /// 参数表达式
            /// </summary>
            // ReSharper disable once InconsistentNaming
            private static readonly ParameterExpression _parameterExpression = Expression.Parameter(typeof(T), "p");

            /// <summary>
            /// 检查字典
            /// </summary>
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once CollectionNeverUpdated.Local
            // ReSharper disable once StaticMemberInGenericType
            private static readonly Dictionary<string, Expression> _check = new Dictionary<string, Expression>();

            /// <summary>
            /// 函数
            /// </summary>
            private static Func<T, T> _func;

            /// <summary>
            /// 复制
            /// </summary>
            /// <param name="source">数据源</param>
            public static T Copy(T source)
            {
                if (_func == null)
                {
                    var memberBindings = new List<MemberBinding>();
                    foreach (var item in GetAllPropertiesOrFields())
                    {
                        if (_check.ContainsKey(item.Name))
                        {
                            var memberBinding = Expression.Bind(item, _check[item.Name]);
                            memberBindings.Add(memberBinding);
                        }
                        else
                        {
                            if (typeof(T).GetProperty(item.Name) != null || typeof(T).GetField(item.Name) != null)
                            {
                                var memberBinding = Expression.Bind(item,
                                    Expression.PropertyOrField(_parameterExpression, item.Name));
                                memberBindings.Add(memberBinding);
                            }
                        }
                    }

                    var memberInitExpression =
                        Expression.MemberInit(Expression.New(typeof(T)), memberBindings.ToArray());
                    var lambda = Expression.Lambda<Func<T, T>>(memberInitExpression, _parameterExpression);
                    _func = lambda.Compile();
                }
                return _func.Invoke(source);
            }

            /// <summary>
            /// 获取所有属性或字段
            /// </summary>
            private static IEnumerable<MemberInfo> GetAllPropertiesOrFields()
            {
                foreach (var item in typeof(T).GetProperties())
                    yield return item;
                foreach (var item in typeof(T).GetFields())
                    yield return item;
            }
        }
    }
}
