using System;
using System.Globalization;
using System.Text;

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

        #region FormatMessage(格式化异常消息)

        /// <summary>
        /// 格式化异常消息
        /// </summary>
        /// <param name="e">异常对象</param>
        /// <param name="isHideStackTrace">是否隐藏异常规模信息</param>
        /// <returns></returns>
        public static string FormatMessage(this Exception e, bool isHideStackTrace = false)
        {
            StringBuilder sb=new StringBuilder();
            int count = 0;
            string appString = string.Empty;
            while (e!=null)
            {
                if (count > 0)
                {
                    appString += "  ";
                }

                sb.AppendLine($"{appString}异常消息：{e.Message}");
                sb.AppendLine($"{appString}异常类型：{e.GetType().FullName}");
                sb.AppendLine($"{appString}异常方法：{(e.TargetSite == null ? null : e.TargetSite.Name)}");
                sb.AppendLine($"{appString}异常源：{e.Source}");
                if (!isHideStackTrace && e.StackTrace != null)
                {
                    sb.AppendLine($"{appString}异常堆栈：{e.StackTrace}");
                }

                if (e.InnerException != null)
                {
                    sb.AppendLine($"{appString}内部异常：");
                    count++;
                }

                e = e.InnerException;
            }

            return sb.ToString();
        }

        #endregion
    }
}
