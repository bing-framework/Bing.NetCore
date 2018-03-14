using System.Globalization;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 系统扩展 - 格式化扩展
    /// </summary>
    public static partial class Extensions
    {
        #region Description(获取布尔值描述)

        /// <summary>
        /// 获取布尔值描述
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string Description(this bool value)
        {
            return value ? "是" : "否";
        }

        /// <summary>
        /// 获取布尔值描述
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string Description(this bool? value)
        {
            return value == null ? "" : Description(value.Value);
        }

        #endregion

        #region FormatInvariant(格式化字符串，不依赖区域性)

        /// <summary>
        /// 格式化字符串，不依赖区域性
        /// </summary>
        /// <param name="format">格式化字符串</param>
        /// <param name="args">参数</param>
        /// <returns></returns>
        public static string FormatInvariant(this string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        #endregion

    }
}
