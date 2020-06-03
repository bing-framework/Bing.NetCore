using System;
using System.Linq;
using System.Reflection;

namespace Bing.Utils.Judgments
{
    /// <summary>
    /// 类型判断
    /// </summary>
    public static class TypeJudgment
    {
        #region IsEnumType(是否枚举类型)

        /// <summary>
        /// 是否枚举类型
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="mayNullable">是否可空</param>
        public static bool IsEnumType<T>(bool mayNullable = false) => mayNullable
            ? (Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)).IsEnum
            : typeof(T).IsEnum;

        /// <summary>
        /// 是否枚举类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="mayNullable">是否可空</param>
        public static bool IsEnumType(Type type, bool mayNullable = false) => mayNullable
            ? (Nullable.GetUnderlyingType(type) ?? type).IsEnum
            : type.IsEnum;

        /// <summary>
        /// 是否枚举类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <param name="mayNullable">是否可空</param>
        public static bool IsEnumType(TypeInfo typeInfo, bool mayNullable = false) => mayNullable
            ? (Nullable.GetUnderlyingType(typeInfo) ?? typeInfo).IsEnum
            : typeInfo.IsEnum;

        #endregion

        #region IsNumericType(是否数值类型)

        /// <summary>
        /// 是否数值类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsNumericType(Type type) =>
            type == typeof(byte)
            || type == typeof(short)
            || type == typeof(int)
            || type == typeof(long)
            || type == typeof(sbyte)
            || type == typeof(ushort)
            || type == typeof(uint)
            || type == typeof(ulong)
            || type == typeof(decimal)
            || type == typeof(double)
            || type == typeof(float);

        /// <summary>
        /// 是否数值类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static bool IsNumericType(TypeInfo typeInfo) => IsNumericType(typeInfo.AsType());

        #endregion

        #region IsNullableType(是否可空类型)

        /// <summary>
        /// 是否可空类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsNullableType(Type type) =>
            type != null
            && type.GetTypeInfo().IsGenericType
            && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        /// <summary>
        /// 是否可空类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static bool IsNullableType(TypeInfo typeInfo) => IsNullableType(typeInfo.AsType());

        #endregion

        #region IsGenericImplementation(是否泛型实现类型)

        /// <summary>
        /// 是否泛型实现类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="genericType">泛型类型</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool IsGenericImplementation(Type type, Type genericType)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));
            if (genericType is null)
                throw new ArgumentNullException(nameof(genericType));
            if (!genericType.IsGenericType)
                return false;
            bool testFlag;
            testFlag = type.GetInterfaces().Any(_checkRawGenericType);
            if (testFlag)
                return true;
            while (type != null && type != TypeClass.ObjectClass)
            {
                testFlag = _checkRawGenericType(type);
                if (testFlag)
                    return true;
                type = type.BaseType;
            }

            return false;

            // 检查原始泛型类型
            // ReSharper disable once InconsistentNaming
            bool _checkRawGenericType(Type test) => genericType == (test.IsGenericType ? test.GetGenericTypeDefinition() : test);
        }

        /// <summary>
        /// 是否泛型实现类型
        /// </summary>
        /// <typeparam name="TGot">类型</typeparam>
        /// <typeparam name="TGeneric">泛型类型</typeparam>
        public static bool IsGenericImplementation<TGot, TGeneric>() => IsGenericImplementation(typeof(TGot), typeof(TGeneric));

        #endregion

    }
}
