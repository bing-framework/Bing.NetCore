using System;

namespace Bing.Offices.Excels.Attributes
{
    /// <summary>
    /// Excel 导出时用于标识ID
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ExcelTargetAttribute:Attribute
    {
        /// <summary>
        /// 定义Excel 导出ID 来确定导出字段
        /// </summary>
        public string Value { get; set; }
    }
}
