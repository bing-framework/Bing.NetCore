using System;
using System.Linq;
using Bing.Helpers;
using Bing.Judgments;

namespace Bing.Reflection
{
    /// <summary>
    /// 类型 操作
    /// </summary>
    public static partial class Types
    {
        #region Of(获取类型)

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        public static Type Of<T>() => Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        /// <summary>
        /// 获取类型数组
        /// </summary>
        /// <param name="objColl">对象数组</param>
        public static Type[] Of(object[] objColl)
        {
            if (objColl is null)
                return null;
            if (!objColl.Contains(null))
                return Type.GetTypeArray(objColl);
            var types = new Type[objColl.Length];
            for (var i = 0; i < objColl.Length; i++)
                types[i] = objColl[i].GetType();
            return types;
        }

        #endregion

        #region DefaultValue(获取默认值)

        /// <summary>
        /// 获取默认值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        public static T DefaultValue<T>() => TypeDefault.Of<T>();

        #endregion

        #region IsGenericImplementation(是否泛型实现类型)

        /// <summary>
        /// 是否泛型实现类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="genericType">泛型类型</param>
        public static bool IsGenericImplementation(Type type, Type genericType) => TypeJudgment.IsGenericImplementation(type, genericType);

        /// <summary>
        /// 是否泛型实现类型
        /// </summary>
        /// <typeparam name="TGot">类型</typeparam>
        /// <typeparam name="TGeneric">泛型类型</typeparam>
        public static bool IsGenericImplementation<TGot, TGeneric>() => TypeJudgment.IsGenericImplementation<TGot, TGeneric>();

        #endregion

        #region GetRawTypeFromGenericClass(获取原始类型)

        /// <summary>
        /// 获取原始类型。当类型从泛型类型中继承时，获取泛型类型中与该类型相对应的第一个类型参数
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="genericType">泛型类型</param>
        public static Type GetRawTypeFromGenericClass(Type type, Type genericType) => Reflections.GetRawTypeFromGenericClass(type, genericType);

        /// <summary>
        /// 获取原始类型。当类型从泛型类型中继承时，获取泛型类型中与该类型相对应的第一个类型参数
        /// </summary>
        /// <typeparam name="TGot">类型</typeparam>
        /// <typeparam name="TGeneric">泛型类型</typeparam>
        public static Type GetRawTypeFromGenericClass<TGot, TGeneric>() => Reflections.GetRawTypeFromGenericClass<TGot, TGeneric>();

        #endregion
    }
}
