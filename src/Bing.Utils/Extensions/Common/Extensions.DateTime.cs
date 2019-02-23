using System;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 系统扩展 - 日期
    /// </summary>
    public static partial class Extensions
    {
        #region ToDateTimeString(yyyy-MM-dd HH:mm:ss)

        /// <summary>
        /// 获取格式化字符串，带时分秒，格式："yyyy-MM-dd HH:mm:ss"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <param name="isRemoveSecond">是否移除秒,true:是,false:否</param>
        /// <returns></returns>
        public static string ToDateTimeString(this DateTime dateTime, bool isRemoveSecond = false)
        {
            return dateTime.ToString(isRemoveSecond ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 获取格式化字符串，带时分秒，格式："yyyy-MM-dd HH:mm:ss"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <param name="isRemoveSecond">是否移除秒,true:是,false:否</param>
        /// <returns></returns>
        public static string ToDateTimeString(this DateTime? dateTime, bool isRemoveSecond = false)
        {
            return dateTime == null ? string.Empty : ToDateTimeString(dateTime.Value, isRemoveSecond);
        }

        #endregion

        #region ToDateString(yyyy-MM-dd)

        /// <summary>
        /// 获取格式化字符串，不带时分秒，格式："yyyy-MM-dd"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static string ToDateString(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// 获取格式化字符串，不带时分秒，格式："yyyy-MM-dd"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static string ToDateString(this DateTime? dateTime)
        {
            return dateTime == null ? string.Empty : ToDateString(dateTime.Value);
        }

        #endregion

        #region ToTimeString(HH:mm:ss)

        /// <summary>
        /// 获取格式化字符串，不带年月日，格式："HH:mm:ss"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static string ToTimeString(this DateTime dateTime)
        {
            return dateTime.ToString("HH:mm:ss");
        }

        /// <summary>
        /// 获取格式化字符串，不带年月日，格式："HH:mm:ss"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static string ToTimeString(this DateTime? dateTime)
        {
            return dateTime == null ? string.Empty : ToTimeString(dateTime.Value);
        }

        #endregion

        #region ToMillisecondString(yyyy-MM-dd HH:mm:ss.fff)

        /// <summary>
        /// 获取格式化字符串，带毫秒，格式："yyyy-MM-dd HH:mm:ss.fff"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static string ToMillisecondString(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// 获取格式化字符串，带毫秒，格式："yyyy-MM-dd HH:mm:ss.fff"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static string ToMillisecondString(this DateTime? dateTime)
        {
            return dateTime == null ? string.Empty : ToMillisecondString(dateTime.Value);
        }

        #endregion

        #region ToChineseDateString(yyyy年MM月dd日)

        /// <summary>
        /// 获取格式化字符串，不带时分秒，格式："yyyy年MM月dd日"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static string ToChineseDateString(this DateTime dateTime)
        {
            return $"{dateTime.Year}年{dateTime.Month}月{dateTime.Day}日";
        }

        /// <summary>
        /// 获取格式化字符串，不带时分秒，格式："yyyy年MM月dd日"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static string ToChineseDateString(this DateTime? dateTime)
        {
            return dateTime == null ? string.Empty : ToChineseDateString(dateTime.Value);
        }

        #endregion

        #region ToChineseDateTimeString(yyyy年MM月dd日 HH时mm分)

        /// <summary>
        /// 获取格式化字符串，带时分秒，格式："yyyy年MM月dd日 HH时mm分"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <param name="isRemoveSecond">是否移除秒</param>
        /// <returns></returns>
        public static string ToChineseDateTimeString(this DateTime dateTime, bool isRemoveSecond = false)
        {
            var result = new StringBuilder();
            result.AppendFormat("{0}年{1}月{2}日", dateTime.Year, dateTime.Month, dateTime.Day);
            result.AppendFormat(" {0}时{1}分", dateTime.Hour, dateTime.Minute);
            if (isRemoveSecond == false)
            {
                result.AppendFormat("{0}秒", dateTime.Second);
            }

            return result.ToString();
        }

        /// <summary>
        /// 获取格式化字符串，带时分秒，格式："yyyy年MM月dd日 HH时mm分"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <param name="isRemoveSecond">是否移除秒</param>
        /// <returns></returns>
        public static string ToChineseDateTimeString(this DateTime? dateTime, bool isRemoveSecond = false)
        {
            return dateTime == null ? string.Empty : ToChineseDateTimeString(dateTime.Value, isRemoveSecond);
        }

        #endregion

        #region Description(获取描述)

        /// <summary>
        /// 获取描述
        /// </summary>
        /// <param name="span">时间间隔</param>
        /// <returns></returns>
        public static string Description(this TimeSpan span)
        {
            var result = new StringBuilder();
            if (span.Days > 0)
            {
                result.AppendFormat("{0}天", span.Days);
            }

            if (span.Hours > 0)
            {
                result.AppendFormat("{0}小时", span.Hours);
            }

            if (span.Minutes > 0)
            {
                result.AppendFormat("{0}分", span.Minutes);
            }

            if (span.Seconds > 0)
            {
                result.AppendFormat("{0}秒", span.Seconds);
            }

            if (span.Milliseconds > 0)
            {
                result.AppendFormat("{0}毫秒", span.Milliseconds);
            }

            if (result.Length > 0)
            {
                return result.ToString();
            }

            return $"{span.TotalSeconds * 1000}毫秒";
        }

        #endregion
    }
}
