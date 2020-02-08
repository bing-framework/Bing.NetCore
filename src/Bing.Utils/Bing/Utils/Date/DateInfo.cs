using System;
using Bing.Extensions;

namespace Bing.Utils.Date
{
    /// <summary>
    /// 日期信息
    /// </summary>
    public class DateInfo
    {
        /// <summary>
        /// 内部日期时间
        /// </summary>
        private DateTime _internalDateTime { get; set; }

        /// <summary>
        /// 初始化一个<see cref="DateInfo"/>类型的实例
        /// </summary>
        internal DateInfo() { }

        public DateInfo(DateTime dt)
        {
            _internalDateTime = dt.SetTime(0, 0, 0, 0);
        }
    }
}
