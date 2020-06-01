using System;
using System.Collections.Generic;
using System.Reflection;
using AspectCore.Extensions.Reflection;

namespace Bing.Helpers
{
    /// <summary>
    /// 反射 操作
    /// </summary>
    public static partial class Reflections
    {
        #region GetAttribute(获取特性)

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="fieldInfo">字段元数据</param>
        public static TAttribute GetAttribute<TAttribute>(FieldInfo fieldInfo) where TAttribute : Attribute =>
            fieldInfo?.GetReflector().GetCustomAttribute<TAttribute>();

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="memberInfo">元数据</param>
        public static TAttribute GetAttribute<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute =>
            memberInfo?.GetCustomAttribute<TAttribute>();

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="type">类型</param>
        public static TAttribute GetAttribute<TAttribute>(Type type) where TAttribute : Attribute =>
            type?.GetReflector().GetCustomAttribute<TAttribute>();

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="typeInfo">类型信息</param>
        public static TAttribute GetAttribute<TAttribute>(TypeInfo typeInfo) where TAttribute : Attribute =>
            typeInfo?.GetReflector().GetCustomAttribute<TAttribute>();

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <param name="fieldInfo">字段信息</param>
        /// <param name="attributeType">特性类型</param>
        public static Attribute GetAttribute(FieldInfo fieldInfo, Type attributeType) =>
            fieldInfo?.GetReflector().GetCustomAttribute(attributeType);

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <param name="memberInfo">元数据</param>
        /// <param name="attributeType">特性类型</param>
        public static Attribute GetAttribute(MemberInfo memberInfo, Type attributeType) =>
            memberInfo?.GetCustomAttribute(attributeType);

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="attributeType">特性类型</param>
        public static Attribute GetAttribute(Type type, Type attributeType) =>
            type?.GetReflector().GetCustomAttribute(attributeType);

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <param name="attributeType">特性类型</param>
        public static Attribute GetAttribute(TypeInfo typeInfo, Type attributeType) =>
            typeInfo?.GetReflector().GetCustomAttribute(attributeType);

        #endregion

        #region GetAttributes(获取特性集合)

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="fieldInfo">字段元数据</param>
        public static IEnumerable<TAttribute> GetAttributes<TAttribute>(FieldInfo fieldInfo) where TAttribute : Attribute =>
            fieldInfo?.GetReflector().GetCustomAttributes<TAttribute>();

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="memberInfo">元数据</param>
        public static IEnumerable<TAttribute> GetAttributes<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute =>
            memberInfo?.GetCustomAttributes<TAttribute>();

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="type">类型</param>
        public static IEnumerable<TAttribute> GetAttributes<TAttribute>(Type type) where TAttribute : Attribute =>
            type?.GetReflector().GetCustomAttributes<TAttribute>();

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="typeInfo">类型信息</param>
        public static IEnumerable<TAttribute> GetAttributes<TAttribute>(TypeInfo typeInfo) where TAttribute : Attribute =>
            typeInfo?.GetReflector().GetCustomAttributes<TAttribute>();

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <param name="fieldInfo">字段信息</param>
        /// <param name="attributeType">特性类型</param>
        public static IEnumerable<Attribute> GetAttributes(FieldInfo fieldInfo, Type attributeType) =>
            fieldInfo?.GetReflector().GetCustomAttributes(attributeType);

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <param name="memberInfo">元数据</param>
        /// <param name="attributeType">特性类型</param>
        public static IEnumerable<Attribute> GetAttributes(MemberInfo memberInfo, Type attributeType) =>
            memberInfo?.GetCustomAttributes(attributeType);

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="attributeType">特性类型</param>
        public static IEnumerable<Attribute> GetAttributes(Type type, Type attributeType) =>
            type?.GetReflector().GetCustomAttributes(attributeType);

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <param name="attributeType">特性类型</param>
        public static IEnumerable<Attribute> GetAttributes(TypeInfo typeInfo, Type attributeType) =>
            typeInfo?.GetReflector().GetCustomAttributes(attributeType);

        #endregion

        #region GetRequiredAttribute(获取必填特性)

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="fieldInfo">字段元数据</param>
        /// <exception cref="ArgumentException"></exception>
        public static TAttribute GetRequiredAttribute<TAttribute>(FieldInfo fieldInfo) where TAttribute : Attribute =>
            fieldInfo?.GetReflector().GetCustomAttribute<TAttribute>() ??
            throw new ArgumentException($"There is no {typeof(TAttribute)} attribute can be found.");

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="memberInfo">元数据</param>
        /// <exception cref="ArgumentException"></exception>
        public static TAttribute GetRequiredAttribute<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute =>
            memberInfo?.GetCustomAttribute<TAttribute>() ??
            throw new ArgumentException($"There is no {typeof(TAttribute)} attribute can be found.");

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="type">类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static TAttribute GetRequiredAttribute<TAttribute>(Type type) where TAttribute : Attribute =>
            type?.GetReflector().GetCustomAttribute<TAttribute>() ??
            throw new ArgumentException($"There is no {typeof(TAttribute)} attribute can be found.");

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="typeInfo">类型信息</param>
        /// <exception cref="ArgumentException"></exception>
        public static TAttribute GetRequiredAttribute<TAttribute>(TypeInfo typeInfo) where TAttribute : Attribute =>
            typeInfo?.GetReflector().GetCustomAttribute<TAttribute>() ??
            throw new ArgumentException($"There is no {typeof(TAttribute)} attribute can be found.");

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <param name="fieldInfo">字段信息</param>
        /// <param name="attributeType">特性类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static Attribute GetRequiredAttribute(FieldInfo fieldInfo, Type attributeType) =>
            fieldInfo?.GetReflector().GetCustomAttribute(attributeType) ??
            throw new ArgumentException($"There is no {attributeType} attribute can be found.");

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <param name="memberInfo">元数据</param>
        /// <param name="attributeType">特性类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static Attribute GetRequiredAttribute(MemberInfo memberInfo, Type attributeType) =>
            memberInfo?.GetCustomAttribute(attributeType) ??
            throw new ArgumentException($"There is no {attributeType} attribute can be found.");

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="attributeType">特性类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static Attribute GetRequiredAttribute(Type type, Type attributeType) =>
            type?.GetReflector().GetCustomAttribute(attributeType) ??
            throw new ArgumentException($"There is no {attributeType} attribute can be found.");

        /// <summary>
        /// 获取特性
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <param name="attributeType">特性类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static Attribute GetRequiredAttribute(TypeInfo typeInfo, Type attributeType) =>
            typeInfo?.GetReflector().GetCustomAttribute(attributeType) ??
            throw new ArgumentException($"There is no {attributeType} attribute can be found.");

        #endregion

        #region GetRequiredAttributes(获取必填特性集合)

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="fieldInfo">字段元数据</param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<TAttribute> GetRequiredAttributes<TAttribute>(FieldInfo fieldInfo) where TAttribute : Attribute =>
            fieldInfo?.GetReflector().GetCustomAttributes<TAttribute>() ??
            throw new ArgumentException($"There is no any {typeof(TAttribute)} attributes can be found.");

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="memberInfo">元数据</param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<TAttribute> GetRequiredAttributes<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute =>
            memberInfo?.GetCustomAttributes<TAttribute>() ??
            throw new ArgumentException($"There is no any {typeof(TAttribute)} attributes can be found.");

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="type">类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<TAttribute> GetRequiredAttributes<TAttribute>(Type type) where TAttribute : Attribute =>
            type?.GetReflector().GetCustomAttributes<TAttribute>() ??
            throw new ArgumentException($"There is no any {typeof(TAttribute)} attributes can be found.");

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <typeparam name="TAttribute">泛型特性</typeparam>
        /// <param name="typeInfo">类型信息</param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<TAttribute> GetRequiredAttributes<TAttribute>(TypeInfo typeInfo) where TAttribute : Attribute =>
            typeInfo?.GetReflector().GetCustomAttributes<TAttribute>() ??
            throw new ArgumentException($"There is no any {typeof(TAttribute)} attributes can be found.");

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <param name="fieldInfo">字段信息</param>
        /// <param name="attributeType">特性类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<Attribute> GetRequiredAttributes(FieldInfo fieldInfo, Type attributeType) =>
            fieldInfo?.GetReflector().GetCustomAttributes(attributeType) ??
            throw new ArgumentException($"There is no any {attributeType} attributes can be found.");

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <param name="memberInfo">元数据</param>
        /// <param name="attributeType">特性类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<Attribute> GetRequiredAttributes(MemberInfo memberInfo, Type attributeType) =>
            memberInfo?.GetCustomAttributes(attributeType) ??
            throw new ArgumentException($"There is no any {attributeType} attributes can be found.");

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="attributeType">特性类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<Attribute> GetRequiredAttributes(Type type, Type attributeType) =>
            type?.GetReflector().GetCustomAttributes(attributeType) ??
            throw new ArgumentException($"There is no any {attributeType} attributes can be found.");

        /// <summary>
        /// 获取特性集合
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <param name="attributeType">特性类型</param>
        /// <exception cref="ArgumentException"></exception>
        public static IEnumerable<Attribute> GetRequiredAttributes(TypeInfo typeInfo, Type attributeType) =>
            typeInfo?.GetReflector().GetCustomAttributes(attributeType) ??
            throw new ArgumentException($"There is no any {attributeType} attributes can be found.");

        #endregion
    }
}
