using System;
using System.Linq;
using System.Reflection;
using AspectCore.Extensions.Reflection;
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

        #region IsEnumType(是否枚举类型)

        /// <summary>
        /// 是否枚举类型
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="mayNullable">是否可空</param>
        public static bool IsEnumType<T>(bool mayNullable = false) => TypeJudgment.IsEnumType<T>(mayNullable);

        /// <summary>
        /// 是否枚举类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="mayNullable">是否可空</param>
        public static bool IsEnumType(Type type, bool mayNullable = false) => TypeJudgment.IsEnumType(type, mayNullable);

        /// <summary>
        /// 是否枚举类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <param name="mayNullable">是否可空</param>
        public static bool IsEnumType(TypeInfo typeInfo, bool mayNullable = false) => TypeJudgment.IsEnumType(typeInfo, mayNullable);

        #endregion

        #region IsNumericType(是否数值类型)

        /// <summary>
        /// 是否数值类型
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public static bool IsNumericType<T>() => TypeJudgment.IsNumericType<T>();

        /// <summary>
        /// 是否数值类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsNumericType(Type type) => TypeJudgment.IsNumericType(type);

        /// <summary>
        /// 是否数值类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static bool IsNumericType(TypeInfo typeInfo) => TypeJudgment.IsNumericType(typeInfo);

        #endregion

        #region IsNullableType(是否可空类型)

        /// <summary>
        /// 是否可空类型
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public static bool IsNullableType<T>() => TypeJudgment.IsNullableType<T>();

        /// <summary>
        /// 是否可空类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsNullableType(Type type) => TypeJudgment.IsNullableType(type);

        /// <summary>
        /// 是否可空类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static bool IsNullableType(TypeInfo typeInfo) => TypeJudgment.IsNullableType(typeInfo);

        #endregion

        #region CreateInstance(创建实例)

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        /// <param name="args">参数</param>
        public static TInstance CreateInstance<TInstance>(params object[] args) =>
            args is null || args.Length == 0
                ? CreateInstanceCore<TInstance>()
                : CreateInstanceCore<TInstance>(args);

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        /// <param name="className">实例名称</param>
        /// <param name="args">参数</param>
        public static TInstance CreateInstance<TInstance>(string className, params object[] args)
        {
            var type = Type.GetType(className) ?? Assembly.GetCallingAssembly().GetType(className);
            return CreateInstance<TInstance>(type, args);
        }

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        /// <param name="type">实例类型</param>
        /// <param name="args">参数</param>
        public static TInstance CreateInstance<TInstance>(Type type, params object[] args) =>
            CreateInstance(type, args) is TInstance ret ? ret : default;

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="type">实例类型</param>
        /// <param name="args">参数</param>
        public static object CreateInstance(Type type, params object[] args) => args is null || args.Length == 0
            ? CreateInstanceCore(type)
            : CreateInstanceCore(type, args);

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        private static TInstance CreateInstanceCore<TInstance>() =>
            CreateInstanceCore(typeof(TInstance)) is TInstance ret ? ret : default;

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        /// <param name="args">参数</param>
        private static TInstance CreateInstanceCore<TInstance>(object[] args) =>
            CreateInstanceCore(typeof(TInstance), args) is TInstance ret ? ret : default;

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="type">实例类型</param>
        private static object CreateInstanceCore(Type type) => type.GetConstructors()
            .FirstOrDefault(x => !x.GetParameters().Any())?.GetReflector().Invoke();

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="type">实例类型</param>
        /// <param name="args">参数</param>
        private static object CreateInstanceCore(Type type, object[] args) => type.GetConstructor(Reflection.Types.Of(args))?.GetReflector().Invoke(args);

        #endregion
    }
}
