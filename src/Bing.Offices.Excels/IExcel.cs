using System.IO;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core.Styles;

namespace Bing.Offices.Excels
{
    /// <summary>
    /// Excel 操作
    /// </summary>
    public interface IExcel
    {
        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <returns></returns>
        IExcel CreateWorkbook();

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <param name="filePath">文件名称，绝对路径</param>
        /// <returns></returns>
        IExcel CreateWorkbook(string filePath);

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <param name="stream">文件流，传递过来的创建的工作簿对象</param>
        /// <returns></returns>
        IExcel CreateWorkbook(Stream stream);

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IExcel CreateSheet(string sheetName = "");

        /// <summary>
        /// 创建单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        IExcel CreateRow(int rowIndex);

        /// <summary>
        /// 创建单元格
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <returns></returns>
        IExcel CreateCell(ICell cell);

        /// <summary>
        /// 写入流
        /// </summary>
        /// <param name="stream">内存流</param>
        /// <returns></returns>
        IExcel Write(Stream stream);

        /// <summary>
        /// 设置日期格式
        /// </summary>
        /// <param name="format">日期格式，默认："yyyy-mm-dd"</param>
        /// <returns></returns>
        IExcel DateFormat(string format = "yyyy-mm-dd");

        /// <summary>
        /// 合并单元格。坐标：(x1,x2,y1,y2)
        /// </summary>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <param name="startColumnIndex">起始列索引</param>
        /// <param name="endColumnIndex">结束列索引</param>
        /// <returns></returns>
        IExcel MergeCell(int startRowIndex, int endRowIndex, int startColumnIndex, int endColumnIndex);

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <returns></returns>
        IExcel MergeCell(ICell cell);

        /// <summary>
        /// 设置标题样式
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        IExcel TitleStyle(IWorkSheet sheet, CellStyle style);

        /// <summary>
        /// 设置表头样式
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        IExcel HeadStyle(IWorkSheet sheet, CellStyle style);

        /// <summary>
        /// 设置正文样式
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        IExcel BodyStyle(IWorkSheet sheet, CellStyle style);

        /// <summary>
        /// 设置页脚样式
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        IExcel FootStyle(IWorkSheet sheet, CellStyle style);

        /// <summary>
        /// 设置单元格列宽
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <param name="width">列宽度，设置字符数</param>
        /// <returns></returns>
        IExcel ColumnWidth(int columnIndex, int width);

        /// <summary>
        /// 设置单元格自动列宽
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        IExcel AutoColumnWidth(int columnIndex);

        /// <summary>
        /// 设置单元格自动列宽
        /// </summary>
        /// <returns></returns>
        IExcel AutoColumnWidth();

        /// <summary>
        /// 设置单元行行高
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="height">行高度</param>
        /// <returns></returns>
        IExcel RowHeight(int rowIndex, int height);

        /// <summary>
        /// 设置单元行自动行高
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        IExcel AutoRowHeight(int rowIndex);

        /// <summary>
        /// 设置单元行自动行高
        /// </summary>
        /// <returns></returns>
        IExcel AutoRowHeight();

        /// <summary>
        /// 获取工作簿
        /// </summary>
        /// <param name="stream">流</param>
        /// <returns></returns>
        IWorkbook GetWorkbook(Stream stream);

        /// <summary>
        /// 获取工作簿
        /// </summary>
        /// <param name="fileName">文件名称，绝对路径</param>
        /// <returns></returns>
        IWorkbook GetWorkbook(string fileName);
    }
}
