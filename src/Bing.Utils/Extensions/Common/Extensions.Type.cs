using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.Helpers;
using Bing.Utils.Helpers;

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
        public static bool IsNullableType(this Type type) => type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        /// <summary>
        /// 是否可空类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="genericParameterType">通用参数类型</param>
        public static bool IsNullableType(this Type type, Type genericParameterType)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            return genericParameterType == Nullable.GetUnderlyingType(type);
        }

        #endregion

        #region IsNullableEnum(是否可空枚举类型)

        /// <summary>
        /// 是否可空枚举类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsNullableEnum(this Type type) => Nullable.GetUnderlyingType(type)?.GetTypeInfo().IsEnum ?? false;

        #endregion

        #region HasAttribute(是否有指定特性)

        /// <summary>
        /// 是否有指定特性
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="type">类型</param>
        /// <param name="inherit">是否允许继承链搜索</param>
        public static bool HasAttribute<T>(this Type type, bool inherit = false) where T : Attribute => type.GetTypeInfo().IsDefined(typeof(T), inherit);

        #endregion

        #region GetAttributes(获取指定特性集合)

        /// <summary>
        /// 获取指定特性集合
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="type">类型</param>
        /// <param name="inherit">是否允许继承链搜索</param>
        public static IEnumerable<T> GetAttributes<T>(this Type type, bool inherit = false) where T : Attribute => type.GetTypeInfo().GetCustomAttributes<T>(inherit);

        #endregion

        #region GetAttribute(获取指定特性)

        /// <summary>
        /// 获取指定特性
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="type">类型</param>
        /// <param name="inherit">是否允许继承链搜索</param>
        public static T GetAttribute<T>(this Type type, bool inherit = false) where T : Attribute => type.GetTypeInfo().GetCustomAttributes<T>(inherit).FirstOrDefault();

        #endregion

        #region IsCustomType(是否自定义类型)

        /// <summary>
        /// 是否自定义类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsCustomType(this Type type)
        {
            if (type.IsPrimitive)
                return false;
            if (type.IsArray && type.HasElementType && type.GetElementType().IsPrimitive)
                return false;
            return type != typeof(object) && type != typeof(Guid) &&
                   Type.GetTypeCode(type) == TypeCode.Object && !type.IsGenericType;
        }

        #endregion

        #region IsAnonymousType(是否匿名类型)

        /// <summary>
        /// 是否匿名类型
        /// </summary>
        /// <param name="type">类型</param>
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
        public static bool IsBaseType(this Type type, Type checkingType)
        {
            while (type != typeof(object))
            {
                if (type == null)
                    continue;
                if (type == checkingType)
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        #endregion

        #region CanUseForDb(能否用于数据库存储)

        /// <summary>
        /// 能否用于数据库存储
        /// </summary>
        /// <param name="type">类型</param>
        public static bool CanUseForDb(this Type type) =>
            type == typeof(string)
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

        #endregion

        #region IsDeriveClassFrom(判断当前类型是否可由指定类型派生)

        /// <summary>
        /// 判断当前类型是否可由指定类型派生
        /// </summary>
        /// <typeparam name="TBaseType">基类型</typeparam>
        /// <param name="type">当前类型</param>
        /// <param name="canAbstract">能否是抽象类</param>
        public static bool IsDeriveClassFrom<TBaseType>(this Type type, bool canAbstract = false) => Reflection.IsDeriveClassFrom<TBaseType>(type, canAbstract);

        /// <summary>
        /// 判断当前类型是否可由指定类型派生
        /// </summary>
        /// <param name="type">当前类型</param>
        /// <param name="baseType">基类型</param>
        /// <param name="canAbstract">能否是抽象类</param>
        public static bool IsDeriveClassFrom(this Type type, Type baseType, bool canAbstract = false) => Reflection.IsDeriveClassFrom(type, baseType, canAbstract);

        #endregion

        #region IsBaseOn(返回当前类型是否是指定基类的派生类)

        /// <summary>
        /// 返回当前类型是否是指定基类的派生类
        /// </summary>
        /// <typeparam name="TBaseType">基类型</typeparam>
        /// <param name="type">类型</param>
        public static bool IsBaseOn<TBaseType>(this Type type) => Reflection.IsBaseOn<TBaseType>(type);

        /// <summary>
        /// 返回当前类型是否是指定基类的派生类
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="baseType">基类类型</param>
        public static bool IsBaseOn(this Type type, Type baseType) => Reflection.IsBaseOn(type, baseType);

        #endregion

        #region IsGenericAssignableFrom(判断当前泛型类型是否可由指定类型的实例填充)

        /// <summary>
        /// 判断当前泛型类型是否可由指定类型的实例填充
        /// </summary>
        /// <param name="genericType">泛型类型</param>
        /// <param name="type">指定类型</param>
        public static bool IsGenericAssignableFrom(this Type genericType, Type type) => Reflection.IsGenericAssignableFrom(genericType, type);

        #endregion

        #region IsIntegerType(是否整数类型)

        /// <summary>
        /// 是否整数类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsIntegerType(this Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region IsCollectionType(是否集合类型)

        /// <summary>
        /// 是否集合类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsCollectionType(this Type type) => type.GetInterfaces().Any(n => n.Name == nameof(IEnumerable));

        #endregion

        #region IsValueType(是否值类型)

        /// <summary>
        /// 是否值类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsValueType(this Type type)
        {
            var result = IsIntegerType(type);
            if (!result)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.String:
                    case TypeCode.Boolean:
                    case TypeCode.Char:
                    case TypeCode.DateTime:
                    case TypeCode.Decimal:
                    case TypeCode.Double:
                    case TypeCode.Empty:
                    case TypeCode.Single:
                        result = true;
                        break;
                    default:
                        result = false;
                        break;
                }
            }
            return result;
        }

        #endregion
    }
}
