using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 系统扩展 - 类型
    /// </summary>
    public static partial class Extensions
    {
        #region IsNullableType(是否可空类型)

        /// <summary>
        /// 是否可空类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        public static bool IsNullableType(this Type type)
        {
            return ((type != null) && type.IsGenericType) && (type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        /// <summary>
        /// 是否可空类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="genericParameterType">通用参数类型</param>
        /// <returns></returns>
        public static bool IsNullableType(this Type type, Type genericParameterType)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return genericParameterType == Nullable.GetUnderlyingType(type);
        }

        #endregion

        #region IsNullableEnum(是否可空枚举类型)

        /// <summary>
        /// 是否可空枚举类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        public static bool IsNullableEnum(this Type type)
        {
            return Nullable.GetUnderlyingType(type)?.GetTypeInfo().IsEnum ?? false;
        }

        #endregion

        #region HasAttribute(是否有指定特性)

        /// <summary>
        /// 是否有指定特性
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="type">类型</param>
        /// <param name="inherit">是否允许继承链搜索</param>
        /// <returns></returns>
        public static bool HasAttribute<T>(this Type type, bool inherit = false) where T : Attribute
        {
            return type.GetTypeInfo().IsDefined(typeof(T), inherit);
        }

        #endregion

        #region GetAttributes(获取指定特性集合)

        /// <summary>
        /// 获取指定特性集合
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="type">类型</param>
        /// <param name="inherit">是否允许继承链搜索</param>
        /// <returns></returns>
        public static IEnumerable<T> GetAttributes<T>(this Type type, bool inherit = false) where T : Attribute
        {
            return type.GetTypeInfo().GetCustomAttributes<T>(inherit);
        }

        #endregion

        #region GetAttribute(获取指定特性)

        /// <summary>
        /// 获取指定特性
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="type">类型</param>
        /// <param name="inherit">是否允许继承链搜索</param>
        /// <returns></returns>
        public static T GetAttribute<T>(this Type type, bool inherit = false) where T : Attribute
        {
            return type.GetTypeInfo().GetCustomAttributes<T>(inherit).FirstOrDefault();
        }

        #endregion

        #region IsCustomType(是否自定义类型)

        /// <summary>
        /// 是否自定义类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        public static bool IsCustomType(this Type type)
        {
            if (type.IsPrimitive)
            {
                return false;
            }

            if (type.IsArray && type.HasElementType && type.GetElementType().IsPrimitive)
            {
                return false;
            }

            return type != typeof(object) && type != typeof(Guid) &&
                   Type.GetTypeCode(type) == TypeCode.Object && !type.IsGenericType;
        }

        #endregion

        #region IsAnonymousType(是否匿名类型)

        /// <summary>
        /// 是否匿名类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        public static bool IsAnonymousType(this Type type)
        {
            const string csharpAnonPrefix = "<>f__AnonymousType";
            const string vbAnonPrefix = "VB$Anonymous";
            var typeName = type.Name;
            return typeName.StartsWith(csharpAnonPrefix) || typeName.StartsWith(vbAnonPrefix);
        }

        #endregion

        #region IsBaseType(是否基类型)

        /// <summary>
        /// 是否基类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="checkingType">检查类型</param>
        /// <returns></returns>
        public static bool IsBaseType(this Type type, Type checkingType)
        {
            while (type!=typeof(object))
            {
                if (type == null)
                {
                    continue;
                }

                if (type == checkingType)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        #endregion

        /// <summary>
        /// 能否用于数据库存储
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        public static bool CanUseForDb(this Type type)
        {
            return type == typeof(string)
                   || type == typeof(int)
                   || type == typeof(long)
                   || type == typeof(uint)
                   || type == typeof(ulong)
                   || type == typeof(float)
                   || type == typeof(double)
                   || type == typeof(Guid)
                   || type == typeof(byte[])
                   || type == typeof(decimal)
                   || type == typeof(char)
                   || type == typeof(bool)
                   || type == typeof(DateTime)
                   || type == typeof(TimeSpan)
                   || type == typeof(DateTimeOffset)
                   || type.GetTypeInfo().IsEnum
                   || Nullable.GetUnderlyingType(type) != null && CanUseForDb(Nullable.GetUnderlyingType(type));
        }
    }
}
