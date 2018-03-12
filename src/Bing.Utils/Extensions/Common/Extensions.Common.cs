using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 系统扩展 - 公共扩展
    /// </summary>
    public static partial class Extensions
    {
        #region SafeValue(安全获取值)
        /// <summary>
        /// 安全获取值，当值为null时，不会抛出异常
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="value">可空值</param>
        /// <returns></returns>
        public static T SafeValue<T>(this T? value) where T : struct
        {
            return value ?? default(T);
        }
        #endregion

        #region Value(获取枚举值)
        /// <summary>
        /// 获取枚举值
        /// </summary>
        /// <param name="instance">枚举实例</param>
        /// <returns></returns>
        public static int Value(this System.Enum instance)
        {
            return Utils.Helpers.Enum.GetValue(instance.GetType(), instance);
        }

        /// <summary>
        /// 获取枚举值
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="instance">枚举实例</param>
        /// <returns></returns>
        public static TResult Value<TResult>(this System.Enum instance)
        {
            return Utils.Helpers.Conv.To<TResult>(Value(instance));
        }
        #endregion

        #region Description(获取枚举描述)
        /// <summary>
        /// 获取枚举描述，使用<see cref="DescriptionAttribute"/>特性设置描述
        /// </summary>
        /// <param name="instance">枚举实例</param>
        /// <returns></returns>
        public static string Description(this System.Enum instance)
        {
            return Utils.Helpers.Enum.GetDescription(instance.GetType(), instance);
        }
        #endregion

        #region Join(转换为用分隔符连接的字符串)

        /// <summary>
        /// 转换为用分隔符连接的字符串
        /// </summary>
        /// <typeparam name="T">集合元素类型</typeparam>
        /// <param name="list">集合</param>
        /// <param name="quotes">引号，默认不带引号，范例：单引号"'"</param>
        /// <param name="separator">分隔符，默认使用逗号分隔</param>
        /// <returns></returns>
        public static string Join<T>(this IEnumerable<T> list, string quotes = "", string separator = ",")
        {
            return Utils.Helpers.Str.Join(list, quotes, separator);
        }

        #endregion
    }
}
