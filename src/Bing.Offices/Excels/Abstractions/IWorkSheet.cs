using System.Collections.Generic;
using System.Data;
using System.Drawing;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 工作表
    /// </summary>
    public interface IWorkSheet
    {
        /// <summary>
        /// 工作表名称
        /// </summary>
        string SheetName { get; }

        /// <summary>
        /// 列数
        /// </summary>
        int ColumnNum { get; }

        /// <summary>
        /// 行数
        /// </summary>
        int RowNum { get; }

        /// <summary>
        /// 获取单元格值
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        string GetCellValue(int rowIndex, int columnIndex);

        /// <summary>
        /// 获取数据表内容
        /// </summary>
        /// <param name="hasHeader">是否包含表头，默认:false</param>
        /// <returns></returns>
        DataTable GetTableContent(bool hasHeader = false);

        /// <summary>
        /// 获取行列范围
        /// </summary>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="startColumnIndex">起始列索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <param name="endColumnIndex">结束列索引</param>
        /// <returns></returns>
        IRange GetRange(int startRowIndex, int startColumnIndex, int endRowIndex, int endColumnIndex);

        /// <summary>
        /// 获取单元格
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        ICell GetCell(int rowIndex, int columnIndex);

        /// <summary>
        /// 获取单元格的计算公式
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        string GetCellFormula(int rowIndex, int columnIndex);

        /// <summary>
        /// 获取单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        IRow GetRow(int rowIndex);

        /// <summary>
        /// 获取单元列
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        IColumn GetColumn(int columnIndex);

        /// <summary>
        /// 插入单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        void InsertRow(int rowIndex);

        /// <summary>
        /// 插入单元列
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        void InsertColumn(int columnIndex);

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        void SetCellValue(string value, int rowIndex, int columnIndex);

        /// <summary>
        /// 设置单元格的计算公式
        /// </summary>
        /// <param name="formula">计算公式</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        void SetCellFormula(string formula, int rowIndex, int columnIndex);

        /// <summary>
        /// 设置指定行列范围颜色
        /// </summary>
        /// <param name="range">行列范围</param>
        /// <param name="color">颜色</param>
        void SetRangeColor(IRange range, Color color);

        /// <summary>
        /// 设置单元格颜色
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <param name="color">颜色</param>
        void SetCellColor(int rowIndex, int columnIndex, Color color);

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <param name="range">行列范围</param>
        void MergeCell(IRange range);

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="startColumnIndex">起始列索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <param name="endColumnIndex">结束列索引</param>
        void MergeCell(int startRowIndex, int startColumnIndex, int endRowIndex, int endColumnIndex);

        /// <summary>
        /// 从单元行中获取数据。横向所有数据
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        List<string> GetDataFromRow(int rowIndex);

        /// <summary>
        /// 从单元列中获取数据。纵向所有数据
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        List<string> GetDataFromColumn(int columnIndex);
    }
}
