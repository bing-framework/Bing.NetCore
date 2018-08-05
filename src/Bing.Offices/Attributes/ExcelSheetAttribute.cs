using System;

namespace Bing.Offices.Attributes
{
    /// <summary>
    /// Excel 工作表
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ExcelSheetAttribute:Attribute
    {
        /// <summary>
        /// 工作表名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 标题行索引
        /// </summary>
        public int TitleRowIndex { get; set; }
        
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 是否自动索引
        /// </summary>
        public bool AutoIndex { get; set; } = true;

        /// <summary>
        /// 工作表索引
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// 初始化一个<see cref="ExcelSheetAttribute"/>类型的实例
        /// </summary>
        /// <param name="name">工作表名</param>
        public ExcelSheetAttribute(string name) : this(name, 0) { }

        /// <summary>
        /// 初始化一个<see cref="ExcelSheetAttribute"/>类型的实例
        /// </summary>
        /// <param name="name">工作表名</param>
        /// <param name="titleRowIndex">标题行索引</param>
        public ExcelSheetAttribute(string name, int titleRowIndex)
        {
            Name = name;
            TitleRowIndex = titleRowIndex;
        }
    }
}
