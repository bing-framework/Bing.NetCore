using System;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Date
{
    /// <summary>
    /// 时间(<see cref="DateTime"/>) 扩展
    /// </summary>
    public static partial class DateTimeExtensions
    {
        #region ToDateTimeString(yyyy-MM-dd HH:mm:ss)

        /// <summary>
        /// 获取格式化字符串，带时分秒，格式："yyyy-MM-dd HH:mm:ss"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <param name="isRemoveSecond">是否移除秒,true:是,false:否</param>
        public static string ToDateTimeString(this DateTime dateTime, bool isRemoveSecond = false) => dateTime.ToString(isRemoveSecond ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// 获取格式化字符串，带时分秒，格式："yyyy-MM-dd HH:mm:ss"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <param name="isRemoveSecond">是否移除秒,true:是,false:否</param>
        public static string ToDateTimeString(this DateTime? dateTime, bool isRemoveSecond = false) => dateTime == null ? string.Empty : ToDateTimeString(dateTime.Value, isRemoveSecond);

        #endregion

        #region ToDateString(yyyy-MM-dd)

        /// <summary>
        /// 获取格式化字符串，不带时分秒，格式："yyyy-MM-dd"
        /// </summary>
        /// <param name="dateTime">日期</param>
        public static string ToDateString(this DateTime dateTime) => dateTime.ToString("yyyy-MM-dd");

        /// <summary>
        /// 获取格式化字符串，不带时分秒，格式："yyyy-MM-dd"
        /// </summary>
        /// <param name="dateTime">日期</param>
        public static string ToDateString(this DateTime? dateTime) => dateTime == null ? string.Empty : ToDateString(dateTime.Value);

        #endregion

        #region ToTimeString(HH:mm:ss)

        /// <summary>
        /// 获取格式化字符串，不带年月日，格式："HH:mm:ss"
        /// </summary>
        /// <param name="dateTime">日期</param>
        public static string ToTimeString(this DateTime dateTime) => dateTime.ToString("HH:mm:ss");

        /// <summary>
        /// 获取格式化字符串，不带年月日，格式："HH:mm:ss"
        /// </summary>
        /// <param name="dateTime">日期</param>
        public static string ToTimeString(this DateTime? dateTime) => dateTime == null ? string.Empty : ToTimeString(dateTime.Value);

        #endregion

        #region ToMillisecondString(yyyy-MM-dd HH:mm:ss.fff)

        /// <summary>
        /// 获取格式化字符串，带毫秒，格式："yyyy-MM-dd HH:mm:ss.fff"
        /// </summary>
        /// <param name="dateTime">日期</param>
        public static string ToMillisecondString(this DateTime dateTime) => dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

        /// <summary>
        /// 获取格式化字符串，带毫秒，格式："yyyy-MM-dd HH:mm:ss.fff"
        /// </summary>
        /// <param name="dateTime">日期</param>
        public static string ToMillisecondString(this DateTime? dateTime) => dateTime == null ? string.Empty : ToMillisecondString(dateTime.Value);

        #endregion

        #region ToChineseDateString(yyyy年MM月dd日)

        /// <summary>
        /// 获取格式化字符串，不带时分秒，格式："yyyy年MM月dd日"
        /// </summary>
        /// <param name="dateTime">日期</param>
        public static string ToChineseDateString(this DateTime dateTime) => $"{dateTime.Year}年{dateTime.Month}月{dateTime.Day}日";

        /// <summary>
        /// 获取格式化字符串，不带时分秒，格式："yyyy年MM月dd日"
        /// </summary>
        /// <param name="dateTime">日期</param>
        public static string ToChineseDateString(this DateTime? dateTime) => dateTime == null ? string.Empty : ToChineseDateString(dateTime.Value);

        #endregion

        #region ToChineseDateTimeString(yyyy年MM月dd日 HH时mm分)

        /// <summary>
        /// 获取格式化字符串，带时分秒，格式："yyyy年MM月dd日 HH时mm分"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <param name="isRemoveSecond">是否移除秒</param>
        public static string ToChineseDateTimeString(this DateTime dateTime, bool isRemoveSecond = false)
        {
            var result = new StringBuilder();
            result.AppendFormat("{0}年{1}月{2}日", dateTime.Year, dateTime.Month, dateTime.Day);
            result.AppendFormat(" {0}时{1}分", dateTime.Hour, dateTime.Minute);
            if (isRemoveSecond == false)
                result.AppendFormat("{0}秒", dateTime.Second);
            return result.ToString();
        }

        /// <summary>
        /// 获取格式化字符串，带时分秒，格式："yyyy年MM月dd日 HH时mm分"
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <param name="isRemoveSecond">是否移除秒</param>
        public static string ToChineseDateTimeString(this DateTime? dateTime, bool isRemoveSecond = false) => dateTime == null ? string.Empty : ToChineseDateTimeString(dateTime.Value, isRemoveSecond);

        #endregion

        #region ToDateTimeOffset(将时间转换为时间点)

        /// <summary>
        /// 将时间转换为时间点
        /// </summary>
        /// <param name="localDateTime">DateTime</param>
        public static DateTimeOffset ToDateTimeOffset(this DateTime localDateTime) =>
            localDateTime.ToDateTimeOffset(null);

        /// <summary>
        /// 将时间转换为时间点
        /// </summary>
        /// <param name="localDateTime">DateTime</param>
        /// <param name="localTimeZone">时区</param>
        public static DateTimeOffset ToDateTimeOffset(this DateTime localDateTime, TimeZoneInfo localTimeZone)
        {
            if (localDateTime.Kind != DateTimeKind.Unspecified)
                localDateTime = new DateTime(localDateTime.Ticks, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(localDateTime, localTimeZone ?? TimeZoneInfo.Local);
        }

        #endregion

        #region ToLocalDateTime(将时间点转换为时间)

        /// <summary>
        /// 将时间点转换为时间
        /// </summary>
        /// <param name="dateTimeUtc">DateTimeOffset</param>
        public static DateTime ToLocalDateTime(this DateTimeOffset dateTimeUtc) => dateTimeUtc.ToLocalDateTime(null);

        /// <summary>
        /// 将时间点转换为时间
        /// </summary>
        /// <param name="dateTimeUtc">DateTimeOffset</param>
        /// <param name="localTimeZone">时区</param>
        public static DateTime ToLocalDateTime(this DateTimeOffset dateTimeUtc, TimeZoneInfo localTimeZone) =>
            TimeZoneInfo.ConvertTime(dateTimeUtc, localTimeZone ?? TimeZoneInfo.Local).DateTime;

        #endregion
    }
}
