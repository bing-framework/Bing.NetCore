using System.Collections.Generic;
using System.IO;
using Bing.Offices.Excels.Abstractions;

namespace Bing.Offices.Excels.Exports
{
    /// <summary>
    /// Excel 导出器
    /// </summary>
    public abstract class ExcelExportBase:ExportBase
    {
        /// <summary>
        /// Excel 操作
        /// </summary>
        private readonly IExcel _excel;

        /// <summary>
        /// 初始化一个<see cref="ExcelExportBase"/>类型的实例
        /// </summary>
        /// <param name="version">Excel 版本</param>
        /// <param name="excel">Excel 操作</param>
        protected ExcelExportBase(ExcelVersion version, IExcel excel) : base(version)
        {
            _excel = excel;
        }

        /// <summary>
        /// 设置单元格列宽
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <param name="width">宽度</param>
        /// <returns></returns>
        public override IExport ColumnWidth(int columnIndex, int width)
        {
            _excel.ColumnWidth(columnIndex, width);
            return this;
        }

        /// <summary>
        /// 设置单元格日期格式
        /// </summary>
        /// <param name="format">日期格式，默认：yyyy-mm-dd</param>
        /// <returns></returns>
        public override IExport DateFormat(string format = "yyyy-mm-dd")
        {
            _excel.DateFormat(format);
            return this;
        }

        /// <summary>
        /// 写入流
        /// </summary>
        /// <param name="stream">流</param>
        protected override void WriteStream(Stream stream)
        {
            AddTitle();
            AddHeader();
            AddBody();
            AddFoot();
            _excel.Write(stream);
        }

        /// <summary>
        /// 添加标题
        /// </summary>
        private void AddTitle()
        {
        }

        /// <summary>
        /// 添加表头
        /// </summary>
        private void AddHeader()
        {
            _excel.HeadStyle(Sheet, GetHeadStyle());
            CreateRows(Sheet.GetHeader());
        }

        /// <summary>
        /// 创建单元行
        /// </summary>
        /// <param name="rows">单元行集合</param>
        private void CreateRows(IEnumerable<IRow> rows)
        {
            foreach (var row in rows)
            {
                _excel.CreateRow(row.RowIndex);
                foreach (var cell in row.Cells)
                {
                    _excel.CreateCell(cell);
                }
            }
        }

        /// <summary>
        /// 添加正文
        /// </summary>
        private void AddBody()
        {
            _excel.BodyStyle(Sheet, GetBodyStyle());
            CreateRows(Sheet.GetBody());
        }

        /// <summary>
        /// 添加页脚
        /// </summary>
        private void AddFoot()
        {
            _excel.FootStyle(Sheet, GetFootStyle());
            CreateRows(Sheet.GetFooter());
        }
    }
}
