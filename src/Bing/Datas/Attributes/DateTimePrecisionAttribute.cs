using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Datas.Attributes
{
    /// <summary>
    /// 自定义 DateTime 类型的精度
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class DateTimePrecisionAttribute : Attribute
    {
        /// <summary>
        /// 精确度
        /// </summary>
        public byte Value { get; set; }

        /// <summary>
        /// 初始化一个<see cref="DateTimePrecisionAttribute"/>类型的实例
        /// </summary>
        /// <param name="value">精确度</param>
        public DateTimePrecisionAttribute(byte value)
        {
            Value = value;
        }
    }
}
