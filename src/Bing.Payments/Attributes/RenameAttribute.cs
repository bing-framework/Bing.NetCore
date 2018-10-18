using System;

namespace Bing.Payments.Attributes
{
    /// <summary>
    /// 重命名 属性
    /// </summary>
    public class RenameAttribute:Attribute
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
    }
}
