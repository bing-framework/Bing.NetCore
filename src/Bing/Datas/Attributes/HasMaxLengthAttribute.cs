using System;

namespace Bing.Datas.Attributes
{
    /// <summary>
    /// 最大长度
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class HasMaxLengthAttribute : Attribute
    {
        /// <summary>
        /// 是否最大长度
        /// </summary>
        public bool HasMaxLength { get; set; } = true;
    }
}
