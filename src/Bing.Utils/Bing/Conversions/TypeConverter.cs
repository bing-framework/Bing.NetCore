using System;
using System.Reflection;
using Bing.Judgments;

namespace Bing.Conversions
{
    /// <summary>
    /// 类型 转换器
    /// </summary>
    public static class TypeConverter
    {
        /// <summary>
        /// 将 可空类型 转换为 非可空基础类型
        /// </summary>
        /// <param name="type">类型</param>
        public static Type ToNonNullableType(Type type) => Nullable.GetUnderlyingType(type);

        /// <summary>
        /// 将 可空类型信息 转换为 非可空基础类型信息
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static TypeInfo ToNonNullableTypeInfo(TypeInfo typeInfo) =>
            Nullable.GetUnderlyingType(typeInfo.AsType()).GetTypeInfo();

        /// <summary>
        /// 将 可空类型 安全转换为 非可空基础类型
        /// </summary>
        /// <param name="type">类型</param>
        public static Type ToSafeNonNullableType(Type type) =>
            TypeJudgment.IsNullableType(type) ? ToNonNullableType(type) : type;

        /// <summary>
        /// 将 可空类型信息 安全转换为 非可空基础类型信息
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static TypeInfo ToSafeNonNullableTypeInfo(TypeInfo typeInfo) =>
            TypeJudgment.IsNullableType(typeInfo) ? ToNonNullableTypeInfo(typeInfo) : typeInfo;
    }
}
