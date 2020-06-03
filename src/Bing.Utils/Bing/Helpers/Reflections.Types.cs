using System;
using System.Reflection;

namespace Bing.Helpers
{
    /// <summary>
    /// 反射 操作
    /// </summary>
    public static partial class Reflections
    {
        #region GetUnderlyingType(获取基础类型)

        /// <summary>
        /// 获取基础类型
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public static Type GetUnderlyingType<T>()
        {
            var type = typeof(T);
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        /// <summary>
        /// 获取基础类型
        /// </summary>
        /// <param name="type">类型</param>
        public static Type GetUnderlyingType(Type type)
        {
            if (type is null)
                return null;
            return Nullable.GetUnderlyingType(type) ?? type;
        }
        
        /// <summary>
        /// 获取基础类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static Type GetUnderlyingType(TypeInfo typeInfo)
        {
            if (typeInfo is null)
                return null;
            var type = typeInfo.AsType();
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        #endregion

        #region GetRawTypeFromGenericClass(获取原始类型)

        /// <summary>
        /// 获取原始类型。当类型从泛型类型中继承时，获取泛型类型中与该类型相对应的第一个类型参数
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="genericType">泛型类型</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static Type GetRawTypeFromGenericClass(Type type, Type genericType)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));
            if(genericType is null)
                throw new ArgumentNullException(nameof(genericType));
            if (!genericType.IsGenericType)
                return null;
            while (type!=null&&type!=TypeClass.ObjectClass)
            {
                var testFlag = _checkRawGenericType(type);
                if (testFlag)
                    return type.GenericTypeArguments.Length > 0 ? type.GenericTypeArguments[0] : null;
                type = type.BaseType;
            }

            return null;

            // 检查原始泛型类型
            // ReSharper disable once InconsistentNaming
            bool _checkRawGenericType(Type test) =>
                genericType == (test.IsGenericType ? test.GetGenericTypeDefinition() : test);
        }

        /// <summary>
        /// 获取原始类型。当类型从泛型类型中继承时，获取泛型类型中与该类型相对应的第一个类型参数
        /// </summary>
        /// <typeparam name="TGot">类型</typeparam>
        /// <typeparam name="TGeneric">泛型类型</typeparam>
        public static Type GetRawTypeFromGenericClass<TGot, TGeneric>() => GetRawTypeFromGenericClass(typeof(TGot), typeof(TGeneric));

        #endregion
    }
}
