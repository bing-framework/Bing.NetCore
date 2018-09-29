using System.Collections.Generic;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 工作表
    /// </summary>
    public interface IWorkSheet
    {
        /// <summary>
        /// 总标题
        /// </summary>
        string Title { get; }

        /// <summary>
        /// 工作表名
        /// </summary>
        string SheetName { get; set; }

        /// <summary>
        /// 列数
        /// </summary>
        int ColumnCount { get; }

        /// <summary>
        /// 总行数
        /// </summary>
        int RowCount { get; }

        /// <summary>
        /// 表头行数
        /// </summary>
        int HeadRowCount { get; }

        /// <summary>
        /// 正文行数
        /// </summary>
        int BodyRowCount { get; }

        /// <summary>
        /// 页脚行数
        /// </summary>
        int FootRowCount { get; }      

        /// <summary>
        /// 获取表头
        /// </summary>
        /// <returns></returns>
        List<IRow> GetHeader();

        /// <summary>
        /// 获取表格正文
        /// </summary>
        /// <returns></returns>
        List<IRow> GetBody();

        /// <summary>
        /// 获取页脚
        /// </summary>
        /// <returns></returns>
        List<IRow> GetFooter();

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="titles">标题</param>
        void AddHeadRow(params string[] titles);

        /// <summary>
        /// 添加表头
        /// </summary>
        /// <param name="cells">单元格</param>
        void AddHeadRow(params ICell[] cells);

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <param name="cellValues">值</param>
        void AddBodyRow(params object[] cellValues);

        /// <summary>
        /// 添加正文
        /// </summary>
        /// <param name="cells">单元格集合</param>
        void AddBodyRow(IEnumerable<ICell> cells);

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="cellValues">值</param>
        void AddFootRow(params string[] cellValues);

        /// <summary>
        /// 添加页脚
        /// </summary>
        /// <param name="cells">单元格集合</param>
        void AddFootRow(params ICell[] cells);

        /// <summary>
        /// 清空表头
        /// </summary>
        void ClearHeader();
    }
}
