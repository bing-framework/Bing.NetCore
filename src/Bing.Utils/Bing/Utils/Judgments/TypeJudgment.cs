using System;
using System.Reflection;

namespace Bing.Utils.Judgments
{
    /// <summary>
    /// 类型判断
    /// </summary>
    public static class TypeJudgment
    {
        /// <summary>
        /// 判断类型是否为数值类型
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
        /// 判断类型是否为数值类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static bool IsNumericType(TypeInfo typeInfo) => IsNumericType(typeInfo.AsType());

        /// <summary>
        /// 判断类型是否为可空类型
        /// </summary>
        /// <param name="type">类型</param>
        public static bool IsNullableType(Type type) =>
            type != null
            && type.GetTypeInfo().IsGenericType
            && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        /// <summary>
        /// 判断类型是否为可空类型
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        public static bool IsNullableType(TypeInfo typeInfo) => IsNullableType(typeInfo.AsType());
    }
}
