using System;

namespace Bing.Offices.Excels.Npoi.Extensions
{
    /// <summary>
    /// 类型扩展
    /// </summary>
    internal static class TypeExtensions
    {
        /// <summary>
        /// 展开<see cref="Nullable"/>类型，如果<paramref name="type"/>为可空类型或者类型为self，则获取可空类型
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        public static Type UnwrapNullableType(this Type type) => Nullable.GetUnderlyingType(type) ?? type;
    }
}
