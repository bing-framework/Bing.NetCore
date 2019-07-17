using System.Collections.Generic;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 定义Excel列头模板
    /// </summary>
    public class ExcelColumnTitleModel
    {
        /// <summary>
        /// 初始化数据
        /// </summary>
        public ExcelColumnTitleModel()
        {
            ListColumn = new List<ExcelColumn>();
        }

        /// <summary>
        /// 整列数据
        /// </summary>
        public List<ExcelColumn> ListColumn { get; set; }
    }

    /// <summary>
    /// 定义单个列头数据
    /// </summary>
    public class ExcelColumn
    {
        /// <summary>
        /// 初始化数据
        /// </summary>
        public ExcelColumn()
        {
            Title = string.Empty;
            NowColIndex = 0;
            Colspan = 1;
            Rowspan = 1;
        }

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 当前列索引
        /// </summary>
        public int NowColIndex { get; set; }

        /// <summary>
        /// 合并列数量
        /// </summary>
        public int Colspan { get; set; }

        /// <summary>
        /// 合并行数量
        /// </summary>
        public int Rowspan { get; set; }
    }
}
