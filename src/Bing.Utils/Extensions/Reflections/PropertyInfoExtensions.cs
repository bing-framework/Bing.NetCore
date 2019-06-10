using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 属性信息(<see cref="PropertyInfo"/>) 扩展
    /// </summary>
    public static class PropertyInfoExtensions
    {
        /// <summary>
        /// 判断属性是否静态
        /// </summary>
        /// <param name="property">属性</param>
        public static bool IsStatic(this PropertyInfo property) => (property.GetMethod ?? property.SetMethod).IsStatic;
    }
}
