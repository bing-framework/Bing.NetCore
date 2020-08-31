namespace System
{
    /// <summary>
    /// 日期时间(<see cref="DateTime"/>) 扩展
    /// </summary>
    public static class BingDateTimeExtensions
    {
        #region Clone(克隆)

        /// <summary>
        /// 克隆
        /// </summary>
        /// <param name="dt">日期时间</param>
        public static DateTime Clone(this DateTime dt) => new DateTime(dt.Ticks, dt.Kind);

        #endregion
    }
}
